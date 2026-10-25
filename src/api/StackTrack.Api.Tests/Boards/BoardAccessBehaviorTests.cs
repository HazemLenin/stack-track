using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using StackTrack.Api.Tests.Persistence;
using StackTrack.Application;
using StackTrack.Application.Auth;
using StackTrack.Application.Boards;
using StackTrack.Application.Errors;
using StackTrack.Domain.Entities;
using StackTrack.Infrastructure.Boards;
using StackTrack.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace StackTrack.Api.Tests.Boards;

public record ReadBoard(Guid BoardId) : IRequest<string>, IBoardScoped;

public record RenameBoard(Guid BoardId) : IRequest<string>, IBoardScoped, IWriteRequest;

public record ReadList(Guid ListId) : IRequest<string>, IListScoped;

public record ReadCard(Guid CardId) : IRequest<string>, ICardScoped;

public record ReadLabel(Guid LabelId) : IRequest<string>, ILabelScoped;

public record ReadComment(Guid CommentId) : IRequest<string>, ICommentScoped;

// Moves a card to another list: touches the card's board and the destination list's board.
public record MoveCard(Guid CardId, Guid ListId) : IRequest<string>, ICardScoped, IListScoped, IWriteRequest;

public class MoveCardHandler(AppDbContext db) : IRequestHandler<MoveCard, string>
{
    public async Task<string> Handle(MoveCard request, CancellationToken ct)
    {
        var card = await db.Cards.SingleAsync(c => c.Id == request.CardId, ct);
        card.ListId = request.ListId;
        await db.SaveChangesAsync(ct);
        return "ok";
    }
}

public class BoardProbeHandlers :
    IRequestHandler<ReadBoard, string>, IRequestHandler<RenameBoard, string>, IRequestHandler<ReadList, string>,
    IRequestHandler<ReadCard, string>, IRequestHandler<ReadLabel, string>, IRequestHandler<ReadComment, string>
{
    public Task<string> Handle(ReadList request, CancellationToken ct) => Task.FromResult("ok");

    public Task<string> Handle(ReadCard request, CancellationToken ct) => Task.FromResult("ok");

    public Task<string> Handle(ReadLabel request, CancellationToken ct) => Task.FromResult("ok");

    public Task<string> Handle(ReadComment request, CancellationToken ct) => Task.FromResult("ok");

    public Task<string> Handle(ReadBoard request, CancellationToken ct) => Task.FromResult("ok");

    public Task<string> Handle(RenameBoard request, CancellationToken ct) => Task.FromResult("ok");
}

[Collection(PostgresCollection.Name)]
public class BoardAccessBehaviorTests(PostgresFixture db)
{
    sealed record FakeUser(Guid Id, bool IsAdmin) : ICurrentUser;

    static User NewUser(UserRole role = UserRole.Member) => new()
    {
        Email = $"{Guid.NewGuid():N}@example.com", DisplayName = "Person", PasswordHash = "hash", Role = role,
    };

    async Task Save(params object[] entities)
    {
        await using var context = db.CreateContext();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    ServiceProvider ProviderFor(User user)
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddMediatR(c => c.RegisterServicesFromAssemblyContaining<BoardProbeHandlers>());
        services.AddScoped(_ => db.CreateContext());
        services.AddSingleton<ICurrentUser>(new FakeUser(user.Id, user.Role == UserRole.Admin));
        services.AddScoped<IBoardAccess, BoardAccess>();
        return services.BuildServiceProvider();
    }

    async Task<string> Send(User user, IRequest<string> request)
    {
        await using var provider = ProviderFor(user);
        return await provider.GetRequiredService<ISender>().Send(request);
    }

    async Task<(User Owner, Board Board)> SeedBoard()
    {
        var owner = NewUser();
        var board = new Board { OwnerId = owner.Id, Name = "Sprint board" };
        await Save(owner, board);
        return (owner, board);
    }

    [Fact]
    public async Task Member_request_reaches_handler()
    {
        var (owner, board) = await SeedBoard();

        (await Send(owner, new RenameBoard(board.Id))).Should().Be("ok");
    }

    [Fact]
    public async Task NonMember_gets_NotFound()
    {
        var (_, board) = await SeedBoard();
        var other = NewUser();
        await Save(other);

        var act = () => Send(other, new ReadBoard(board.Id));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task NonMember_and_unknown_board_errors_are_identical()
    {
        var (_, board) = await SeedBoard();
        var other = NewUser();
        await Save(other);

        var denied = await FluentActions.Awaiting(() => Send(other, new ReadBoard(board.Id))).Should().ThrowAsync<NotFoundException>();
        var unknown = await FluentActions.Awaiting(() => Send(other, new ReadBoard(Guid.NewGuid()))).Should().ThrowAsync<NotFoundException>();

        denied.Which.Message.Should().Be(unknown.Which.Message);
        denied.Which.Message.Should().NotContain(board.Id.ToString());
    }

    [Fact]
    public async Task Admin_non_member_can_read_but_write_returns_403()
    {
        var (_, board) = await SeedBoard();
        var admin = NewUser(UserRole.Admin);
        await Save(admin);

        (await Send(admin, new ReadBoard(board.Id))).Should().Be("ok");
        await FluentActions.Awaiting(() => Send(admin, new RenameBoard(board.Id))).Should().ThrowAsync<ForbiddenException>();
    }

    // A board with one list, card, label and comment, owned by the returned user.
    async Task<(User Owner, Dictionary<string, IRequest<string>> Requests)> SeedChildren()
    {
        var (owner, board) = await SeedBoard();
        var list = new BoardList { BoardId = board.Id, Name = "To do" };
        var card = new Card { ListId = list.Id, Title = "Write docs" };
        var label = new Label { BoardId = board.Id, Name = "Bug", Color = "#ff0000" };
        var comment = new Comment { CardId = card.Id, AuthorId = owner.Id, Body = "On it" };
        await Save(list, label);
        await Save(card);
        await Save(comment);
        return (owner, new()
        {
            ["list"] = new ReadList(list.Id),
            ["card"] = new ReadCard(card.Id),
            ["label"] = new ReadLabel(label.Id),
            ["comment"] = new ReadComment(comment.Id),
        });
    }

    [Theory]
    [InlineData("list")]
    [InlineData("card")]
    [InlineData("label")]
    [InlineData("comment")]
    public async Task NonMember_gets_NotFound_for_list_card_label_comment_ids(string kind)
    {
        var (owner, requests) = await SeedChildren();
        var other = NewUser();
        await Save(other);

        (await Send(owner, requests[kind])).Should().Be("ok");
        await FluentActions.Awaiting(() => Send(other, requests[kind])).Should().ThrowAsync<NotFoundException>();
    }

    [Theory]
    [InlineData("list")]
    [InlineData("card")]
    [InlineData("label")]
    [InlineData("comment")]
    public async Task Unknown_child_id_gets_NotFound(string kind)
    {
        var (owner, _) = await SeedChildren();
        IRequest<string> request = kind switch
        {
            "list" => new ReadList(Guid.NewGuid()),
            "card" => new ReadCard(Guid.NewGuid()),
            "label" => new ReadLabel(Guid.NewGuid()),
            _ => new ReadComment(Guid.NewGuid()),
        };

        await FluentActions.Awaiting(() => Send(owner, request)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Move_into_inaccessible_board_is_rejected_and_card_stays()
    {
        var (_, other) = await SeedBoard();
        var (_, foreignBoard) = await SeedBoard();
        var sourceList = new BoardList { BoardId = other.Id, Name = "Doing" };
        var foreignList = new BoardList { BoardId = foreignBoard.Id, Name = "Done" };
        var member = NewUser();
        var card = new Card { ListId = sourceList.Id, Title = "Ship it" };
        await Save(member, sourceList, foreignList);
        await Save(card, new BoardMember { BoardId = other.Id, UserId = member.Id, JoinedAt = DateTimeOffset.UtcNow });

        await FluentActions.Awaiting(() => Send(member, new MoveCard(card.Id, foreignList.Id))).Should().ThrowAsync<NotFoundException>();

        await using var context = db.CreateContext();
        (await context.Cards.SingleAsync(c => c.Id == card.Id)).ListId.Should().Be(sourceList.Id);
        (await Send(member, new MoveCard(card.Id, sourceList.Id))).Should().Be("ok");
    }
}

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

public record ReadBoardQuery(Guid BoardId) : IRequest<string>, IBoardScoped;

public record RenameBoardCommand(Guid BoardId) : IRequest<string>, IBoardScoped, IWriteRequest;

public record ReadListQuery(Guid ListId) : IRequest<string>, IListScoped;

public record ReadCardQuery(Guid CardId) : IRequest<string>, ICardScoped;

public record ReadLabelQuery(Guid LabelId) : IRequest<string>, ILabelScoped;

public record ReadCommentQuery(Guid CommentId) : IRequest<string>, ICommentScoped;

// Moves a card to another list: touches the card's board and the destination list's board.
public record MoveCardCommand(Guid CardId, Guid ListId) : IRequest<string>, ICardScoped, IListScoped, IWriteRequest;

public class MoveCardCommandHandler(AppDbContext db) : IRequestHandler<MoveCardCommand, string>
{
    public async Task<string> Handle(MoveCardCommand request, CancellationToken ct)
    {
        var card = await db.Cards.SingleAsync(c => c.Id == request.CardId, ct);
        card.ListId = request.ListId;
        await db.SaveChangesAsync(ct);
        return "ok";
    }
}

public class BoardProbeHandlers :
    IRequestHandler<ReadBoardQuery, string>, IRequestHandler<RenameBoardCommand, string>, IRequestHandler<ReadListQuery, string>,
    IRequestHandler<ReadCardQuery, string>, IRequestHandler<ReadLabelQuery, string>, IRequestHandler<ReadCommentQuery, string>
{
    public Task<string> Handle(ReadListQuery request, CancellationToken ct) => Task.FromResult("ok");

    public Task<string> Handle(ReadCardQuery request, CancellationToken ct) => Task.FromResult("ok");

    public Task<string> Handle(ReadLabelQuery request, CancellationToken ct) => Task.FromResult("ok");

    public Task<string> Handle(ReadCommentQuery request, CancellationToken ct) => Task.FromResult("ok");

    public Task<string> Handle(ReadBoardQuery request, CancellationToken ct) => Task.FromResult("ok");

    public Task<string> Handle(RenameBoardCommand request, CancellationToken ct) => Task.FromResult("ok");
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

        (await Send(owner, new RenameBoardCommand(board.Id))).Should().Be("ok");
    }

    [Fact]
    public async Task NonMember_gets_NotFound()
    {
        var (_, board) = await SeedBoard();
        var other = NewUser();
        await Save(other);

        var act = () => Send(other, new ReadBoardQuery(board.Id));

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task NonMember_and_unknown_board_errors_are_identical()
    {
        var (_, board) = await SeedBoard();
        var other = NewUser();
        await Save(other);

        var denied = await FluentActions.Awaiting(() => Send(other, new ReadBoardQuery(board.Id))).Should().ThrowAsync<NotFoundException>();
        var unknown = await FluentActions.Awaiting(() => Send(other, new ReadBoardQuery(Guid.NewGuid()))).Should().ThrowAsync<NotFoundException>();

        denied.Which.Message.Should().Be(unknown.Which.Message);
        denied.Which.Message.Should().NotContain(board.Id.ToString());
    }

    [Fact]
    public async Task Admin_non_member_can_read_but_write_returns_403()
    {
        var (_, board) = await SeedBoard();
        var admin = NewUser(UserRole.Admin);
        await Save(admin);

        (await Send(admin, new ReadBoardQuery(board.Id))).Should().Be("ok");
        await FluentActions.Awaiting(() => Send(admin, new RenameBoardCommand(board.Id))).Should().ThrowAsync<ForbiddenException>();
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
            ["list"] = new ReadListQuery(list.Id),
            ["card"] = new ReadCardQuery(card.Id),
            ["label"] = new ReadLabelQuery(label.Id),
            ["comment"] = new ReadCommentQuery(comment.Id),
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
            "list" => new ReadListQuery(Guid.NewGuid()),
            "card" => new ReadCardQuery(Guid.NewGuid()),
            "label" => new ReadLabelQuery(Guid.NewGuid()),
            _ => new ReadCommentQuery(Guid.NewGuid()),
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

        await FluentActions.Awaiting(() => Send(member, new MoveCardCommand(card.Id, foreignList.Id))).Should().ThrowAsync<NotFoundException>();

        await using var context = db.CreateContext();
        (await context.Cards.SingleAsync(c => c.Id == card.Id)).ListId.Should().Be(sourceList.Id);
        (await Send(member, new MoveCardCommand(card.Id, sourceList.Id))).Should().Be("ok");
    }
}

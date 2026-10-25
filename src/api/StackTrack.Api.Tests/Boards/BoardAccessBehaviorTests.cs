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

namespace StackTrack.Api.Tests.Boards;

public record ReadBoard(Guid BoardId) : IRequest<string>, IBoardScoped;

public record RenameBoard(Guid BoardId) : IRequest<string>, IBoardScoped, IWriteRequest;

public class BoardProbeHandlers : IRequestHandler<ReadBoard, string>, IRequestHandler<RenameBoard, string>
{
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
}

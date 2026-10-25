using FluentAssertions;
using StackTrack.Api.Tests.Persistence;
using StackTrack.Application.Auth;
using StackTrack.Application.Boards;
using StackTrack.Domain.Entities;
using StackTrack.Infrastructure.Boards;

namespace StackTrack.Api.Tests.Boards;

[Collection(PostgresCollection.Name)]
public class BoardAccessTests(PostgresFixture db)
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

    async Task<BoardAccessLevel> AccessFor(User user, Guid boardId)
    {
        await using var context = db.CreateContext();
        return await new BoardAccess(context, new FakeUser(user.Id, user.Role == UserRole.Admin)).GetAsync(boardId, default);
    }

    async Task<(User Owner, Board Board)> SeedBoard()
    {
        var owner = NewUser();
        var board = new Board { OwnerId = owner.Id, Name = "Sprint board" };
        await Save(owner, board);
        return (owner, board);
    }

    [Fact]
    public async Task Owner_gets_full()
    {
        var (owner, board) = await SeedBoard();

        (await AccessFor(owner, board.Id)).Should().Be(BoardAccessLevel.Full);
    }

    [Fact]
    public async Task Member_gets_full()
    {
        var (_, board) = await SeedBoard();
        var member = NewUser();
        await Save(member, new BoardMember { BoardId = board.Id, UserId = member.Id, JoinedAt = DateTimeOffset.UtcNow });

        (await AccessFor(member, board.Id)).Should().Be(BoardAccessLevel.Full);
    }

    [Fact]
    public async Task Admin_who_is_not_a_member_gets_read_only()
    {
        var (_, board) = await SeedBoard();
        var admin = NewUser(UserRole.Admin);
        await Save(admin);

        (await AccessFor(admin, board.Id)).Should().Be(BoardAccessLevel.ReadOnly);
    }

    [Fact]
    public async Task Admin_who_is_a_member_gets_full()
    {
        var (_, board) = await SeedBoard();
        var admin = NewUser(UserRole.Admin);
        await Save(admin, new BoardMember { BoardId = board.Id, UserId = admin.Id, JoinedAt = DateTimeOffset.UtcNow });

        (await AccessFor(admin, board.Id)).Should().Be(BoardAccessLevel.Full);
    }

    [Fact]
    public async Task Non_member_gets_none()
    {
        var (_, board) = await SeedBoard();
        var other = NewUser();
        await Save(other);

        (await AccessFor(other, board.Id)).Should().Be(BoardAccessLevel.None);
    }

    [Fact]
    public async Task Unknown_board_gets_none_even_for_admin()
    {
        var admin = NewUser(UserRole.Admin);
        await Save(admin);

        (await AccessFor(admin, Guid.NewGuid())).Should().Be(BoardAccessLevel.None);
    }
}

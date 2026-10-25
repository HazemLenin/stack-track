using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using StackTrack.Domain.Entities;

namespace StackTrack.Api.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class ConstraintTests(PostgresFixture db)
{
    const string UniqueViolation = "23505";
    const string RestrictViolation = "23001";

    [Fact]
    public async Task Email_unique_ignoring_case()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await Save(NewUser($"Person.{suffix}@Example.com"));

        var duplicate = () => Save(NewUser($"person.{suffix}@example.COM"));

        await AssertSqlState(duplicate, UniqueViolation);
    }

    [Fact]
    public async Task Label_name_unique_per_board_ignoring_case_but_reusable_across_boards()
    {
        var (_, board) = await SeedBoard();
        var (_, otherBoard) = await SeedBoard();
        await Save(new Label { BoardId = board.Id, Name = "Bug", Color = "#ff0000" });

        var duplicate = () => Save(new Label { BoardId = board.Id, Name = "bUG", Color = "#00ff00" });
        var otherBoardSameName = () => Save(new Label { BoardId = otherBoard.Id, Name = "Bug", Color = "#ff0000" });

        await AssertSqlState(duplicate, UniqueViolation);
        await otherBoardSameName.Should().NotThrowAsync();
    }

    [Fact]
    public async Task One_membership_per_user_per_board()
    {
        var (user, board) = await SeedBoard();
        await Save(new BoardMember { BoardId = board.Id, UserId = user.Id, JoinedAt = DateTimeOffset.UtcNow });

        var duplicate = () => Save(new BoardMember { BoardId = board.Id, UserId = user.Id, JoinedAt = DateTimeOffset.UtcNow });

        await AssertSqlState(duplicate, UniqueViolation);
    }

    [Fact]
    public async Task One_assignment_per_user_per_card()
    {
        var graph = await SeedGraph();
        await Save(new CardAssignee { CardId = graph.Card.Id, UserId = graph.User.Id });

        var duplicate = () => Save(new CardAssignee { CardId = graph.Card.Id, UserId = graph.User.Id });

        await AssertSqlState(duplicate, UniqueViolation);
    }

    [Fact]
    public async Task A_label_is_applied_to_a_card_once()
    {
        var graph = await SeedGraph();
        await Save(new CardLabel { CardId = graph.Card.Id, LabelId = graph.Label.Id });

        var duplicate = () => Save(new CardLabel { CardId = graph.Card.Id, LabelId = graph.Label.Id });

        await AssertSqlState(duplicate, UniqueViolation);
    }

    [Fact]
    public async Task Refresh_token_hash_is_unique()
    {
        var (user, _) = await SeedBoard();
        var hash = Guid.NewGuid().ToString("N");
        await Save(new RefreshToken { UserId = user.Id, TokenHash = hash, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });

        var duplicate = () => Save(new RefreshToken { UserId = user.Id, TokenHash = hash, ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) });

        await AssertSqlState(duplicate, UniqueViolation);
    }

    [Fact]
    public async Task Board_delete_cascades_but_keeps_users()
    {
        var graph = await SeedGraph();
        await Save(new BoardMember { BoardId = graph.Board.Id, UserId = graph.User.Id, JoinedAt = DateTimeOffset.UtcNow });
        await Save(new CardAssignee { CardId = graph.Card.Id, UserId = graph.User.Id });
        await Save(new CardLabel { CardId = graph.Card.Id, LabelId = graph.Label.Id });
        await Save(new Comment { CardId = graph.Card.Id, AuthorId = graph.User.Id, Body = "hi" });

        await using var context = db.CreateContext();
        await context.Boards.Where(b => b.Id == graph.Board.Id).ExecuteDeleteAsync();

        (await context.BoardLists.AnyAsync(x => x.BoardId == graph.Board.Id)).Should().BeFalse();
        (await context.Cards.AnyAsync(x => x.Id == graph.Card.Id)).Should().BeFalse();
        (await context.Labels.AnyAsync(x => x.BoardId == graph.Board.Id)).Should().BeFalse();
        (await context.BoardMembers.AnyAsync(x => x.BoardId == graph.Board.Id)).Should().BeFalse();
        (await context.CardAssignees.AnyAsync(x => x.CardId == graph.Card.Id)).Should().BeFalse();
        (await context.CardLabels.AnyAsync(x => x.CardId == graph.Card.Id)).Should().BeFalse();
        (await context.Comments.AnyAsync(x => x.CardId == graph.Card.Id)).Should().BeFalse();
        (await context.Users.AnyAsync(x => x.Id == graph.User.Id)).Should().BeTrue();
    }

    [Fact]
    public async Task User_with_a_board_cannot_be_deleted()
    {
        var (user, _) = await SeedBoard();

        var delete = async () =>
        {
            await using var context = db.CreateContext();
            context.Users.Remove(user);
            await context.SaveChangesAsync();
        };

        await AssertSqlState(delete, RestrictViolation);
    }

    [Fact]
    public async Task Deleting_the_replacing_token_nulls_the_reference()
    {
        var (user, _) = await SeedBoard();
        var replacement = new RefreshToken { UserId = user.Id, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1) };
        var old = new RefreshToken
        {
            UserId = user.Id, TokenHash = Guid.NewGuid().ToString("N"), ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
            ReplacedByTokenId = replacement.Id,
        };
        await Save(replacement, old);

        await using var context = db.CreateContext();
        await context.RefreshTokens.Where(t => t.Id == replacement.Id).ExecuteDeleteAsync();

        (await context.RefreshTokens.AsNoTracking().SingleAsync(t => t.Id == old.Id)).ReplacedByTokenId.Should().BeNull();
    }

    internal async Task Save(params object[] entities)
    {
        await using var context = db.CreateContext();
        context.AddRange(entities);
        await context.SaveChangesAsync();
    }

    internal async Task<(User User, Board Board)> SeedBoard()
    {
        var user = NewUser($"{Guid.NewGuid():N}@example.com");
        var board = new Board { OwnerId = user.Id, Name = "Board" };
        await Save(user, board);
        return (user, board);
    }

    internal async Task<(User User, Board Board, BoardList List, Card Card, Label Label)> SeedGraph()
    {
        var (user, board) = await SeedBoard();
        var list = new BoardList { BoardId = board.Id, Name = "List", Position = 1000 };
        var card = new Card { ListId = list.Id, Title = "Card", Position = 1000 };
        var label = new Label { BoardId = board.Id, Name = "Label", Color = "#123456" };
        await Save(list, card, label);
        return (user, board, list, card, label);
    }

    static User NewUser(string email) => new() { Email = email, DisplayName = "Person", PasswordHash = "hash" };

    static async Task AssertSqlState(Func<Task> action, string sqlState)
    {
        var thrown = await action.Should().ThrowAsync<DbUpdateException>();
        thrown.Which.InnerException.Should().BeOfType<PostgresException>().Which.SqlState.Should().Be(sqlState);
    }
}

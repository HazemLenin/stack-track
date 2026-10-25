using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StackTrack.Domain.Entities;

namespace StackTrack.Api.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class TimestampTimezoneTests(PostgresFixture db)
{
    [Fact]
    public async Task NonUtcOffset_roundtrips_same_instant()
    {
        // 15:00 in Cairo summer time (UTC+3) is 12:00 UTC.
        var cairo = new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.FromHours(3));
        var (cardId, _) = await SeedCard(cairo);

        await using var read = db.CreateContext();
        var stored = (await read.Cards.FindAsync(cardId))!.DueDate!.Value;

        stored.Should().Be(cairo);
        stored.UtcDateTime.Should().Be(new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc));
        stored.Offset.Should().Be(TimeSpan.Zero);

        var type = await read.Database.SqlQuery<string>(
            $"SELECT pg_typeof(due_date)::text AS \"Value\" FROM card WHERE id = {cardId}").SingleAsync();
        type.Should().Be("timestamp with time zone");
    }

    async Task<(Guid CardId, Guid ListId)> SeedCard(DateTimeOffset dueDate)
    {
        await using var context = db.CreateContext();
        var user = new User { Email = $"{Guid.NewGuid()}@example.com", DisplayName = "Tz", PasswordHash = "h" };
        var board = new Board { OwnerId = user.Id, Name = "b" };
        var list = new BoardList { BoardId = board.Id, Name = "l", Position = 1000 };
        var card = new Card { ListId = list.Id, Title = "c", Position = 1000, DueDate = dueDate };
        context.AddRange(user, board, list, card);
        await context.SaveChangesAsync();
        return (card.Id, list.Id);
    }
}

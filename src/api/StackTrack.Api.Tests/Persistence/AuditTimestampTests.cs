using FluentAssertions;
using StackTrack.Domain.Entities;

namespace StackTrack.Api.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class AuditTimestampTests(PostgresFixture db)
{
    static readonly DateTimeOffset T0 = new(2026, 6, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Insert_sets_equal_utc_timestamps()
    {
        var clock = new FakeClock(T0);
        var user = NewUser();

        await using (var write = db.CreateContext(clock))
        {
            write.Users.Add(user);
            await write.SaveChangesAsync();
        }

        user.CreatedAt.Should().Be(T0);
        user.UpdatedAt.Should().Be(T0);
        await using var read = db.CreateContext();
        var stored = await read.Users.FindAsync(user.Id);
        stored!.CreatedAt.Should().Be(T0);
        stored.UpdatedAt.Should().Be(T0);
        stored.CreatedAt.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task Update_changes_only_updated_at()
    {
        var clock = new FakeClock(T0);
        var user = NewUser();
        await using var context = db.CreateContext(clock);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        clock.Now = T0.AddHours(1);
        user.DisplayName = "Renamed";
        await context.SaveChangesAsync();

        await using var read = db.CreateContext();
        var stored = await read.Users.FindAsync(user.Id);
        stored!.DisplayName.Should().Be("Renamed");
        stored.CreatedAt.Should().Be(T0);
        stored.UpdatedAt.Should().Be(T0.AddHours(1));
    }

    [Fact]
    public async Task Update_ignores_handler_supplied_created_at()
    {
        var clock = new FakeClock(T0);
        var user = NewUser();
        await using var context = db.CreateContext(clock);
        context.Users.Add(user);
        await context.SaveChangesAsync();

        clock.Now = T0.AddHours(1);
        user.CreatedAt = T0.AddYears(-10);
        user.DisplayName = "Renamed";
        await context.SaveChangesAsync();

        await using var read = db.CreateContext();
        (await read.Users.FindAsync(user.Id))!.CreatedAt.Should().Be(T0);
    }

    static User NewUser() => new()
    {
        Email = $"{Guid.NewGuid()}@example.com",
        DisplayName = "Audit",
        PasswordHash = "hash",
    };

    sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

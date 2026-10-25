using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StackTrack.Domain.Entities;

namespace StackTrack.Api.Tests.Persistence;

[Collection(PostgresCollection.Name)]
public class UserLockoutColumnsTests(PostgresFixture db)
{
    static User NewUser() => new() { Email = $"{Guid.NewGuid()}@example.com", DisplayName = "L", PasswordHash = "h" };

    [Fact]
    public async Task New_user_has_no_failed_attempts_and_no_lock()
    {
        var user = NewUser();
        await using (var write = db.CreateContext())
        {
            write.Add(user);
            await write.SaveChangesAsync();
        }

        await using var read = db.CreateContext();
        var stored = await read.Users.SingleAsync(u => u.Id == user.Id);
        stored.FailedLoginCount.Should().Be(0);
        stored.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task LockedUntil_roundtrips_the_same_instant_for_a_non_utc_offset()
    {
        var cairo = new DateTimeOffset(2026, 6, 1, 15, 0, 0, TimeSpan.FromHours(3));
        var user = NewUser();
        user.FailedLoginCount = 5;
        user.LockedUntil = cairo;
        await using (var write = db.CreateContext())
        {
            write.Add(user);
            await write.SaveChangesAsync();
        }

        await using var read = db.CreateContext();
        var stored = await read.Users.SingleAsync(u => u.Id == user.Id);
        stored.FailedLoginCount.Should().Be(5);
        stored.LockedUntil.Should().Be(cairo);
        stored.LockedUntil!.Value.Offset.Should().Be(TimeSpan.Zero);
    }
}

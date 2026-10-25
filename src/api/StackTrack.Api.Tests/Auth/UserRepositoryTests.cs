using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using StackTrack.Api.Tests.Persistence;
using StackTrack.Domain.Entities;
using StackTrack.Infrastructure.Auth;

namespace StackTrack.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class UserRepositoryTests(PostgresFixture db)
{
    static User NewUser(string email) => new() { Email = email, DisplayName = "T", PasswordHash = "x" };

    [Fact]
    public async Task TryAdd_returns_false_when_another_writer_took_the_email_first()
    {
        var email = $"race-{Guid.NewGuid():N}@example.com";
        await using var first = db.CreateContext();
        await using var second = db.CreateContext();

        (await new UserRepository(first).TryAddAsync(NewUser(email), default)).Should().BeTrue();
        // Same email in different case, from a context that never saw the first insert.
        var lost = NewUser(email.ToUpperInvariant());
        (await new UserRepository(second).TryAddAsync(lost, default)).Should().BeFalse();

        second.Entry(lost).State.Should().Be(EntityState.Detached);
        await using var check = db.CreateContext();
        (await check.Users.CountAsync(u => u.Email.ToLower() == email)).Should().Be(1);
    }

    [Fact]
    public void Refresh_cookie_path_defaults_to_the_public_api_prefix() =>
        new JwtOptions().RefreshCookiePath.Should().Be("/api/auth");
}

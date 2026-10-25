using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StackTrack.Api.Tests.Persistence;
using StackTrack.Domain.Entities;
using StackTrack.Infrastructure.Auth;

namespace StackTrack.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class TokenServiceTests(PostgresFixture db)
{
    static readonly JwtOptions Options = new()
    {
        Secret = "test-secret-test-secret-test-secret-0123456789",
        Issuer = "tests",
        Audience = "tests",
    };

    [Fact]
    public async Task Refresh_token_is_stored_as_hash_only_and_expires_in_7_days()
    {
        var now = new DateTimeOffset(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);
        var user = new User { Email = $"t-{Guid.NewGuid():N}@example.com", DisplayName = "T", PasswordHash = "x" };
        await using (var seed = db.CreateContext())
        {
            seed.Add(user);
            await seed.SaveChangesAsync();
        }

        await using var context = db.CreateContext();
        var service = new TokenService(context, Microsoft.Extensions.Options.Options.Create(Options), new FixedClock(now));

        var issued = await service.IssueAsync(user, CancellationToken.None);

        var stored = await context.Set<RefreshToken>().SingleAsync(t => t.UserId == user.Id);
        stored.TokenHash.Should().NotBe(issued.RefreshToken);
        stored.TokenHash.Should().Be(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(issued.RefreshToken))));
        stored.ExpiresAt.Should().Be(now.AddDays(7));
        issued.RefreshTokenExpiresAt.Should().Be(now.AddDays(7));
        issued.Response.AccessTokenExpiresAt.Should().Be(now.AddMinutes(15));
    }

    sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}

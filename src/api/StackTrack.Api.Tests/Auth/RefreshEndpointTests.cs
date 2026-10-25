using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using StackTrack.Api.Tests.Persistence;
using StackTrack.Api.Tests.TestSupport;
using StackTrack.Domain.Entities;
using StackTrack.Infrastructure.Auth;

namespace StackTrack.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class RefreshEndpointTests(PostgresFixture db)
{
    static readonly DateTimeOffset Start = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    readonly FakeClock _clock = new(Start);

    ApiFactory Factory() => new(connectionString: db.ConnectionString, clock: _clock);

    // No cookie jar: the test sets the refresh cookie by hand, so old tokens can be replayed.
    static HttpClient RawClient(ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    static string CookieValue(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("refresh_token="))
            .Split(';')[0]["refresh_token=".Length..];

    static Task<HttpResponseMessage> Refresh(HttpClient client, string? token)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/auth/refresh");
        if (token is not null) request.Headers.Add("Cookie", $"refresh_token={token}");
        return client.SendAsync(request);
    }

    // Registers a user and returns the email and the first refresh token.
    static async Task<(string Email, string Token)> Register(HttpClient client)
    {
        var email = $"user-{Guid.NewGuid():N}@example.com";
        var response = await client.PostAsJsonAsync("/auth/register",
            new { displayName = "Sara", email, password = "Valid1234", confirmPassword = "Valid1234" });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return (email, CookieValue(response));
    }

    async Task<RefreshToken> Stored(string rawToken)
    {
        await using var context = db.CreateContext();
        var hash = TokenService.Hash(rawToken);
        return await context.RefreshTokens.SingleAsync(t => t.TokenHash == hash);
    }

    [Fact]
    public async Task Refresh_rotates_and_revokes_old_token()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (_, first) = await Register(client);

        var response = await Refresh(client, first);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrEmpty();
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();
        var second = CookieValue(response);
        second.Should().NotBe(first);

        var oldRow = await Stored(first);
        var newRow = await Stored(second);
        oldRow.IsRevoked.Should().BeTrue();
        oldRow.ReplacedByTokenId.Should().Be(newRow.Id);
        newRow.IsRevoked.Should().BeFalse();
        newRow.ExpiresAt.Should().Be(Start.AddDays(7));
    }

    [Fact]
    public async Task Refresh_does_not_extend_the_original_seven_day_window()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (_, first) = await Register(client);
        _clock.Advance(TimeSpan.FromDays(3));

        var second = CookieValue(await Refresh(client, first));

        (await Stored(second)).ExpiresAt.Should().Be(Start.AddDays(7));
    }

    [Fact]
    public async Task Refresh_after_seven_days_returns_401()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (_, a) = await Register(client);
        _clock.Advance(TimeSpan.FromDays(3));
        var b = CookieValue(await Refresh(client, a));
        _clock.Advance(TimeSpan.FromDays(3));
        var c = CookieValue(await Refresh(client, b));
        _clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromSeconds(1));

        (await Refresh(client, c)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_without_a_cookie_or_with_an_unknown_token_returns_401()
    {
        using var factory = Factory();
        using var client = RawClient(factory);

        var none = await Refresh(client, null);
        var unknown = await Refresh(client, "not-a-real-token");

        none.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await unknown.Content.ReadAsStringAsync()).Should().Contain("Your session has expired. Please sign in again.");
    }

    [Fact]
    public async Task Refresh_with_an_expired_token_returns_401()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (_, token) = await Register(client);
        _clock.Advance(TimeSpan.FromDays(7) + TimeSpan.FromSeconds(1));

        (await Refresh(client, token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_after_sign_out_returns_401()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (_, token) = await Register(client);
        var signOut = new HttpRequestMessage(HttpMethod.Post, "/auth/sign-out");
        signOut.Headers.Add("Cookie", $"refresh_token={token}");
        await client.SendAsync(signOut);

        (await Refresh(client, token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SignOut_leaves_another_session_signed_in()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (email, laptop) = await Register(client);
        var signIn = await client.PostAsJsonAsync("/auth/sign-in", new { email, password = "Valid1234" });
        signIn.StatusCode.Should().Be(HttpStatusCode.OK);
        var phone = CookieValue(signIn);

        var signOut = new HttpRequestMessage(HttpMethod.Post, "/auth/sign-out");
        signOut.Headers.Add("Cookie", $"refresh_token={laptop}");
        (await client.SendAsync(signOut)).StatusCode.Should().Be(HttpStatusCode.NoContent);

        (await Refresh(client, laptop)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Refresh(client, phone)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_reuse_revokes_chain()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (_, first) = await Register(client);
        var second = CookieValue(await Refresh(client, first));
        _clock.Advance(TimeSpan.FromMinutes(1));

        (await Refresh(client, first)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await Refresh(client, second)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await Stored(second)).IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task Refresh_reuse_revokes_every_hop_of_a_longer_chain()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (_, a) = await Register(client);
        _clock.Advance(TimeSpan.FromMinutes(1));
        var b = CookieValue(await Refresh(client, a));
        _clock.Advance(TimeSpan.FromMinutes(1));
        var c = CookieValue(await Refresh(client, b));
        _clock.Advance(TimeSpan.FromMinutes(1));

        (await Refresh(client, a)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await Stored(b)).IsRevoked.Should().BeTrue();
        (await Stored(c)).IsRevoked.Should().BeTrue();
        (await Refresh(client, c)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Immediate_replay_revokes_the_new_token()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (_, first) = await Register(client);
        var second = CookieValue(await Refresh(client, first));

        (await Refresh(client, first)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        (await Stored(second)).IsRevoked.Should().BeTrue();
        (await Refresh(client, second)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_failure_to_claim_leaves_no_orphan_token()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (email, token) = await Register(client);

        await Task.WhenAll(Refresh(client, token), Refresh(client, token), Refresh(client, token));

        await using var context = db.CreateContext();
        var user = await context.Users.SingleAsync(u => u.Email == email);
        var rows = await context.RefreshTokens.Where(t => t.UserId == user.Id).ToListAsync();
        // The original plus exactly one successor, whatever the interleaving.
        rows.Should().HaveCount(2);
        rows.Count(t => !t.IsRevoked).Should().Be(1);
    }

    [Fact]
    public async Task Refresh_cookie_longer_than_256_chars_returns_400()
    {
        using var factory = Factory();
        using var client = RawClient(factory);

        (await Refresh(client, new string('a', 300))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refresh_inactive_user_returns_401()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (email, token) = await Register(client);
        await using (var context = db.CreateContext())
        {
            var user = await context.Users.SingleAsync(u => u.Email == email);
            user.IsActive = false;
            await context.SaveChangesAsync();
        }

        (await Refresh(client, token)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_concurrent_same_token_only_one_succeeds()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var (_, token) = await Register(client);

        var results = await Task.WhenAll(Refresh(client, token), Refresh(client, token));

        results.Select(r => r.StatusCode).Should()
            .BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Unauthorized]);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using StackTrack.Api.Tests.Persistence;
using StackTrack.Api.Tests.TestSupport;
using StackTrack.Domain.Entities;

namespace StackTrack.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class SignInEndpointTests(PostgresFixture db)
{
    const string Password = "Valid1234";
    static readonly DateTimeOffset Start = new(2026, 10, 25, 12, 0, 0, TimeSpan.Zero);

    readonly FakeClock _clock = new(Start);

    ApiFactory Factory() => new(connectionString: db.ConnectionString, clock: _clock);

    static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    static async Task<string> Register(HttpClient client)
    {
        var email = NewEmail();
        var response = await client.PostAsJsonAsync("/auth/register",
            new { displayName = "Sara Ahmed", email, password = Password, confirmPassword = Password });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return email;
    }

    static Task<HttpResponseMessage> SignIn(HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/auth/sign-in", new { email, password });

    async Task<User> Find(string email)
    {
        await using var context = db.CreateContext();
        return await context.Users.SingleAsync(u => u.Email == email.ToLowerInvariant());
    }

    async Task Deactivate(string email)
    {
        await using var context = db.CreateContext();
        var user = await context.Users.SingleAsync(u => u.Email == email);
        user.IsActive = false;
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task SignIn_returns_tokens_with_required_claims()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);

        var response = await SignIn(client, email);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var raw = await response.Content.ReadAsStringAsync();
        raw.Should().NotContain(Password).And.NotContainEquivalentOf("refreshToken").And.NotContainEquivalentOf("passwordHash");
        var body = JsonDocument.Parse(raw).RootElement;
        var user = await Find(email);
        var jwt = new JsonWebToken(body.GetProperty("accessToken").GetString());
        jwt.GetClaim("sub").Value.Should().Be(user.Id.ToString());
        jwt.GetClaim("email").Value.Should().Be(email);
        jwt.GetClaim("name").Value.Should().Be("Sara Ahmed");
        jwt.GetClaim("role").Value.Should().Be("Member");
        new DateTimeOffset(jwt.ValidTo).Should().Be(Start.AddMinutes(15));
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("refresh_token="))
            .ToLowerInvariant().Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict");
    }

    [Fact]
    public async Task SignIn_email_is_case_insensitive()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);

        (await SignIn(client, email.ToUpperInvariant())).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task SignIn_wrong_password_and_unknown_email_return_identical_401_body()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);

        var wrong = await SignIn(client, email, "Wrong12345");
        var unknown = await SignIn(client, NewEmail());

        wrong.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        unknown.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var (a, b) = (await Json(wrong), await Json(unknown));
        a.GetProperty("detail").GetString().Should().Be("Email or password is incorrect");
        foreach (var field in new[] { "title", "status", "detail", "type" })
            a.GetProperty(field).ToString().Should().Be(b.GetProperty(field).ToString());
    }

    [Fact]
    public async Task SignIn_deactivated_with_correct_password_returns_403()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);
        await Deactivate(email);

        var response = await SignIn(client, email);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await Json(response)).GetProperty("detail").GetString()
            .Should().Be("Your account has been deactivated. Please contact an administrator.");
    }

    [Fact]
    public async Task SignIn_deactivated_with_wrong_password_stays_generic()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);
        await Deactivate(email);

        (await SignIn(client, email, "Wrong12345")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Fifth_wrong_attempt_locks_for_15_minutes()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);

        for (var i = 0; i < 4; i++)
            (await SignIn(client, email, "Wrong12345")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var fifth = await SignIn(client, email, "Wrong12345");

        fifth.StatusCode.Should().Be(HttpStatusCode.Locked);
        var body = await Json(fifth);
        body.GetProperty("lockedUntil").GetDateTimeOffset().Should().Be(Start.AddMinutes(15));
        body.GetProperty("detail").GetString().Should().Contain("locked");
        (await Find(email)).LockedUntil.Should().Be(Start.AddMinutes(15));
    }

    [Fact]
    public async Task Parallel_wrong_guesses_still_trip_the_lock()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);

        var results = await Task.WhenAll(Enumerable.Range(0, 12).Select(_ => SignIn(client, email, "Wrong12345")));

        // A request whose own guess did not trip the lock may still answer "locked" if a sibling did.
        results.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Unauthorized || r.StatusCode == HttpStatusCode.Locked);
        results.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).Should().BeLessThanOrEqualTo(4);
        results.Count(r => r.StatusCode == HttpStatusCode.Locked).Should().BeGreaterThanOrEqualTo(8);
        var user = await Find(email);
        user.FailedLoginCount.Should().Be(5);
        user.LockedUntil.Should().Be(Start.AddMinutes(15));
    }

    [Fact]
    public async Task Locked_account_rejects_correct_password()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);
        for (var i = 0; i < 5; i++) await SignIn(client, email, "Wrong12345");

        var response = await SignIn(client, email);

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
    }

    [Fact]
    public async Task Wrong_passwords_while_locked_do_not_extend_lock()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);
        for (var i = 0; i < 5; i++) await SignIn(client, email, "Wrong12345");
        _clock.Advance(TimeSpan.FromMinutes(5));

        var response = await SignIn(client, email, "Wrong12345");

        response.StatusCode.Should().Be(HttpStatusCode.Locked);
        var user = await Find(email);
        user.LockedUntil.Should().Be(Start.AddMinutes(15));
        user.FailedLoginCount.Should().Be(5);
    }

    [Fact]
    public async Task Lock_expires_after_15_minutes_and_success_resets_the_counter()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);
        for (var i = 0; i < 5; i++) await SignIn(client, email, "Wrong12345");
        _clock.Advance(TimeSpan.FromMinutes(15) + TimeSpan.FromSeconds(1));

        (await SignIn(client, email)).StatusCode.Should().Be(HttpStatusCode.OK);

        var user = await Find(email);
        user.FailedLoginCount.Should().Be(0);
        user.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task After_the_lock_expires_a_wrong_password_counts_from_one()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);
        for (var i = 0; i < 5; i++) await SignIn(client, email, "Wrong12345");
        _clock.Advance(TimeSpan.FromMinutes(16));

        (await SignIn(client, email, "Wrong12345")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        var user = await Find(email);
        user.FailedLoginCount.Should().Be(1);
        user.LockedUntil.Should().BeNull();
    }

    [Fact]
    public async Task Success_before_the_fifth_failure_resets_the_counter()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = await Register(client);
        for (var i = 0; i < 4; i++) await SignIn(client, email, "Wrong12345");

        (await SignIn(client, email)).StatusCode.Should().Be(HttpStatusCode.OK);

        (await Find(email)).FailedLoginCount.Should().Be(0);
        (await SignIn(client, email, "Wrong12345")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Unknown_email_is_never_locked()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = NewEmail();

        for (var i = 0; i < 7; i++)
            (await SignIn(client, email)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [InlineData("", "x")]
    [InlineData("a@b.com", "")]
    public async Task SignIn_empty_fields_return_400(string email, string password)
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();

        (await SignIn(client, email, password)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

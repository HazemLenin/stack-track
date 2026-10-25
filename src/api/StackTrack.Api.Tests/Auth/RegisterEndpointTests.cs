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
public class RegisterEndpointTests(PostgresFixture db)
{
    static string Email() => $"user-{Guid.NewGuid():N}@example.com";

    ApiFactory Factory() => new(connectionString: db.ConnectionString);

    static object Body(string email, string name = "Sara Ahmed", string password = "Valid1234", string? confirm = null, object? role = null) =>
        role is null
            ? new { displayName = name, email, password, confirmPassword = confirm ?? password }
            : new { displayName = name, email, password, confirmPassword = confirm ?? password, role };

    static async Task<JsonElement> Json(HttpResponseMessage r) =>
        JsonDocument.Parse(await r.Content.ReadAsStringAsync()).RootElement;

    async Task<User> Find(string email)
    {
        await using var context = db.CreateContext();
        return await context.Set<User>().SingleAsync(u => u.Email == email.ToLowerInvariant().Trim());
    }

    [Fact]
    public async Task Register_creates_member_and_returns_tokens()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = Email();

        var response = await client.PostAsJsonAsync("/auth/register", Body(email));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await Find(email);
        user.Role.Should().Be(UserRole.Member);
        user.IsActive.Should().BeTrue();

        var body = await Json(response);
        body.GetProperty("user").GetProperty("role").GetString().Should().Be("Member");
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();

        var jwt = new JsonWebToken(body.GetProperty("accessToken").GetString());
        jwt.GetClaim("sub").Value.Should().Be(user.Id.ToString());
        jwt.GetClaim("email").Value.Should().Be(user.Email);
        jwt.GetClaim("name").Value.Should().Be("Sara Ahmed");
        jwt.GetClaim("role").Value.Should().Be("Member");
        (jwt.ValidTo - DateTime.UtcNow).Should().BeCloseTo(TimeSpan.FromMinutes(15), TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Register_sets_refresh_cookie_with_security_flags()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/auth/register", Body(Email()));

        var cookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("refresh_token="));
        cookie.Should().Contain("httponly").And.Contain("secure").And.Contain("samesite=strict").And.Contain("path=/auth");
        cookie.ToLowerInvariant().Should().Contain("expires=");
    }

    [Fact]
    public async Task Register_ignores_role_field_in_body()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = Email();

        var response = await client.PostAsJsonAsync("/auth/register", Body(email, role: "Admin"));

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        (await Find(email)).Role.Should().Be(UserRole.Member);
    }

    [Fact]
    public async Task Register_duplicate_email_ignoring_case_returns_409_and_creates_no_row()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = Email();
        await client.PostAsJsonAsync("/auth/register", Body(email));

        var response = await client.PostAsJsonAsync("/auth/register", Body(email.ToUpperInvariant()));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await Json(response)).GetProperty("detail").GetString().Should().Be("An account with this email already exists");
        await using var context = db.CreateContext();
        (await context.Set<User>().CountAsync(u => u.Email == email)).Should().Be(1);
    }

    [Fact]
    public async Task Register_normalizes_email()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = Email();

        await client.PostAsJsonAsync("/auth/register", Body("  " + email.ToUpperInvariant() + "  "));

        (await Find(email)).Email.Should().Be(email);
    }

    [Fact]
    public async Task Register_trims_display_name()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = Email();

        await client.PostAsJsonAsync("/auth/register", Body(email, name: "  Sara  "));

        (await Find(email)).DisplayName.Should().Be("Sara");
    }

    [Fact]
    public async Task Register_stores_hashed_password()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = Email();

        await client.PostAsJsonAsync("/auth/register", Body(email, password: "Valid1234"));

        var hash = (await Find(email)).PasswordHash;
        hash.Should().NotBe("Valid1234").And.NotContain("Valid1234");
    }

    [Fact]
    public async Task Register_concurrent_same_email_yields_one_201_and_one_409()
    {
        using var factory = Factory();
        using var a = factory.CreateApiClient();
        using var b = factory.CreateApiClient();
        var email = Email();

        var results = await Task.WhenAll(
            a.PostAsJsonAsync("/auth/register", Body(email)),
            b.PostAsJsonAsync("/auth/register", Body(email)));

        results.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.Created, HttpStatusCode.Conflict]);
    }

    [Fact]
    public async Task Register_invalid_returns_400_per_field()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsJsonAsync("/auth/register", Body(Email(), password: "weak", confirm: "other"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var errors = (await Json(response)).GetProperty("errors");
        errors.TryGetProperty("Password", out _).Should().BeTrue();
        errors.TryGetProperty("ConfirmPassword", out _).Should().BeTrue();
    }

    [Fact]
    public async Task SignOut_revokes_token_and_clears_cookie()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();
        var email = Email();
        await client.PostAsJsonAsync("/auth/register", Body(email));
        var userId = (await Find(email)).Id;

        var response = await client.PostAsync("/auth/sign-out", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith("refresh_token=")).Should().Contain("expires=Thu, 01 Jan 1970");
        await using var context = db.CreateContext();
        (await context.Set<RefreshToken>().Where(t => t.UserId == userId).AllAsync(t => t.IsRevoked)).Should().BeTrue();
    }

    [Fact]
    public async Task SignOut_without_cookie_is_a_noop_204()
    {
        using var factory = Factory();
        using var client = factory.CreateApiClient();

        var response = await client.PostAsync("/auth/sign-out", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}

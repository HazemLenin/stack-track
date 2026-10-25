using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using StackTrack.Api.Tests.Persistence;
using StackTrack.Api.Tests.TestSupport;

namespace StackTrack.Api.Tests.Auth;

[Collection(PostgresCollection.Name)]
public class AuthorizationTests(PostgresFixture db)
{
    ApiFactory Factory() => new(connectionString: db.ConnectionString);

    static HttpClient RawClient(ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = false });

    static async Task<string> RegisterAndGetToken(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/auth/register", new
        {
            displayName = "Sara",
            email = $"user-{Guid.NewGuid():N}@example.com",
            password = "Valid1234",
            confirmPassword = "Valid1234",
        });
        response.StatusCode.Should().Be(HttpStatusCode.Created);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("accessToken").GetString()!;
    }

    [Fact]
    public async Task Protected_endpoint_returns_401_without_token()
    {
        using var factory = Factory();
        using var client = RawClient(factory);

        (await client.GetAsync("/test/protected")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Protected_endpoint_returns_200_with_valid_token()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        var token = await RegisterAndGetToken(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        (await client.GetAsync("/test/protected")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Protected_endpoint_returns_401_with_garbage_token()
    {
        using var factory = Factory();
        using var client = RawClient(factory);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");

        (await client.GetAsync("/test/protected")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // Sign-in and refresh can legitimately answer 401 themselves, so a rejection by the
    // authentication layer is told apart by its WWW-Authenticate challenge.
    [Theory]
    [InlineData("/auth/register")]
    [InlineData("/auth/sign-in")]
    [InlineData("/auth/refresh")]
    [InlineData("/auth/sign-out")]
    public async Task Auth_endpoints_stay_open_to_anonymous(string path)
    {
        using var factory = Factory();
        using var client = RawClient(factory);

        var response = await client.PostAsJsonAsync(path, new { });

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.Headers.WwwAuthenticate.Should().BeEmpty();
    }

    [Fact]
    public async Task Health_stays_open_to_anonymous()
    {
        using var factory = Factory();
        using var client = RawClient(factory);

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}

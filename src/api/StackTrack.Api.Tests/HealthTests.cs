using System.Net;
using FluentAssertions;
using StackTrack.Api.Tests.TestSupport;
using Testcontainers.PostgreSql;

namespace StackTrack.Api.Tests;

public class HealthTests
{
    [Fact]
    public async Task Health_returns_503_and_database_unhealthy_when_db_unreachable()
    {
        using var factory = new ApiFactory(connectionString: "Host=localhost;Port=1;Timeout=2;Username=x;Password=x;Database=x");
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        var body = await ErrorHandlingTests.ReadJson(response);
        body.GetProperty("status").GetString().Should().Be("Unhealthy");
        body.GetProperty("checks").GetProperty("database").GetString().Should().Be("Unhealthy");
    }
}

public class HealthWithDatabaseTests : IAsyncLifetime
{
    readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    [Fact]
    public async Task Health_returns_200_and_database_healthy_when_db_reachable()
    {
        using var factory = new ApiFactory(connectionString: _postgres.GetConnectionString());
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("X-Correlation-Id").Should().BeTrue();
        var body = await ErrorHandlingTests.ReadJson(response);
        body.GetProperty("status").GetString().Should().Be("Healthy");
        body.GetProperty("checks").GetProperty("database").GetString().Should().Be("Healthy");
    }
}

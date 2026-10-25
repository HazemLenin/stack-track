using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using StackTrack.Api.Tests.TestSupport;

namespace StackTrack.Api.Tests;

public class ErrorHandlingTests
{
    const string ProblemJson = "application/problem+json";

    [Fact]
    public async Task Validation_failure_returns_400_problem_details_with_per_field_errors()
    {
        using var client = new ApiFactory().CreateClient();

        var response = await client.PostAsJsonAsync("/test/validate", new { name = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        var body = await ReadJson(response);
        body.GetProperty("status").GetInt32().Should().Be(400);
        body.GetProperty("errors").GetProperty("Name").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Class_level_validation_failure_without_property_name_returns_400()
    {
        using var client = new ApiFactory().CreateClient();

        var response = await client.PostAsJsonAsync("/test/validate", new { name = "forbidden" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await ReadJson(response)).GetProperty("errors").EnumerateObject().Should().NotBeEmpty();
    }

    [Fact]
    public async Task Malformed_json_returns_400_problem_details()
    {
        using var client = new ApiFactory().CreateClient();
        using var content = new StringContent("{bad", Encoding.UTF8, "application/json");

        var response = await client.PostAsync("/test/validate", content);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        (await ReadJson(response)).GetProperty("traceId").GetString()
            .Should().Be(response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Unknown_route_returns_404_problem_details()
    {
        using var client = new ApiFactory().CreateClient();

        var response = await client.GetAsync("/does-not-exist");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        (await ReadJson(response)).GetProperty("traceId").GetString()
            .Should().Be(response.Headers.GetValues("X-Correlation-Id").Single());
    }

    [Fact]
    public async Task Unhandled_exception_in_Production_returns_500_without_stack_or_message()
    {
        using var client = new ApiFactory("Production").CreateClient();

        var response = await client.GetAsync("/test/boom");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        response.Content.Headers.ContentType!.MediaType.Should().Be(ProblemJson);
        var text = await response.Content.ReadAsStringAsync();
        text.Should().NotContain("secret detail").And.NotContain("StackTrace").And.NotContain("   at ");
        (await ReadJson(response)).GetProperty("status").GetInt32().Should().Be(500);
    }

    [Fact]
    public async Task Unhandled_exception_in_Development_includes_detail()
    {
        using var client = new ApiFactory("Development").CreateClient();

        var response = await client.GetAsync("/test/boom");

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        (await response.Content.ReadAsStringAsync()).Should().Contain("secret detail");
    }

    internal static async Task<JsonElement> ReadJson(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
}

using System.Text.RegularExpressions;
using FluentAssertions;
using StackTrack.Api.Tests.TestSupport;

namespace StackTrack.Api.Tests;

public class CorrelationIdTests
{
    const string Header = "X-Correlation-Id";
    static readonly Regex Allowed = new("^[A-Za-z0-9_-]{1,64}$");

    [Fact]
    public async Task Response_has_generated_correlation_id_and_body_traceId_matches()
    {
        using var client = new ApiFactory().CreateClient();

        var response = await client.GetAsync("/test/boom");

        var id = response.Headers.GetValues(Header).Single();
        id.Should().MatchRegex(Allowed.ToString());
        var body = await ErrorHandlingTests.ReadJson(response);
        body.GetProperty("traceId").GetString().Should().Be(id);
    }

    [Fact]
    public async Task Valid_inbound_id_is_echoed()
    {
        using var client = new ApiFactory().CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/boom");
        request.Headers.Add(Header, "abc-123");

        var response = await client.SendAsync(request);

        response.Headers.GetValues(Header).Single().Should().Be("abc-123");
    }

    [Theory]
    [InlineData("0123456789012345678901234567890123456789012345678901234567890123X")] // 65 chars
    [InlineData("has space")]
    [InlineData("semi;colon")]
    public async Task Junk_inbound_id_is_replaced(string junk)
    {
        using var client = new ApiFactory().CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/test/boom");
        request.Headers.TryAddWithoutValidation(Header, junk);

        var response = await client.SendAsync(request);

        var id = response.Headers.GetValues(Header).Single();
        id.Should().NotBe(junk);
        id.Should().MatchRegex(Allowed.ToString());
    }

    [Fact]
    public async Task Correlation_id_appears_in_server_log_scope_for_the_request()
    {
        var logs = new CapturingLoggerProvider();
        using var client = new ApiFactory(loggerProvider: logs).CreateClient();

        var response = await client.GetAsync("/test/boom");

        var id = response.Headers.GetValues(Header).Single();
        var errors = logs.Entries.Where(e => e.Level == Microsoft.Extensions.Logging.LogLevel.Error).ToList();
        errors.Should().NotBeEmpty();
        errors.Should().OnlyContain(e => e.Scopes.OfType<IEnumerable<KeyValuePair<string, object>>>()
            .Any(s => s.Any(kv => kv.Key == "CorrelationId" && (string)kv.Value == id)));
    }
}

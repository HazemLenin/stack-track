using System.Text.RegularExpressions;

namespace StackTrack.Api.Middleware;

public partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ItemKey = "CorrelationId";

    // Inbound ids are echoed into headers and logs, so only accept a safe, short shape.
    [GeneratedRegex(@"^[A-Za-z0-9_-]{1,64}\z")]
    private static partial Regex Allowed();

    public async Task Invoke(HttpContext context)
    {
        var inbound = context.Request.Headers[HeaderName].ToString();
        var id = Allowed().IsMatch(inbound) ? inbound : Guid.NewGuid().ToString("N");

        context.Items[ItemKey] = id;
        context.TraceIdentifier = id;
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        using (logger.BeginScope(new Dictionary<string, object> { [ItemKey] = id }))
            await next(context);
    }
}

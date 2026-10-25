using Microsoft.Extensions.Logging;

namespace StackTrack.Api.Tests.TestSupport;

public record LogEntry(LogLevel Level, string Message, IReadOnlyList<object?> Scopes);

public class CapturingLoggerProvider : ILoggerProvider, ISupportExternalScope
{
    readonly List<LogEntry> _entries = [];
    IExternalScopeProvider _scopes = new LoggerExternalScopeProvider();

    public IReadOnlyList<LogEntry> Entries
    {
        get { lock (_entries) return _entries.ToList(); }
    }

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopes = scopeProvider;

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose() { }

    class CapturingLogger(CapturingLoggerProvider owner) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            owner._scopes.Push(state);

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scopes = new List<object?>();
            owner._scopes.ForEachScope((scope, list) => list.Add(scope), scopes);
            lock (owner._entries)
                owner._entries.Add(new LogEntry(logLevel, formatter(state, exception), scopes));
        }
    }
}

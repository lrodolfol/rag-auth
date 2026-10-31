using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

namespace RagAuth.IntegrationTests.Infrastructure;

public sealed record CapturedLogEntry(
    LogLevel Level,
    string Category,
    string Message,
    string? ExceptionText,
    IReadOnlyList<object?> Scopes)
{
    public string? CorrelationId => Scopes
        .OfType<IEnumerable<KeyValuePair<string, object?>>>()
        .SelectMany(scope => scope)
        .Where(pair => pair.Key == "CorrelationId")
        .Select(pair => pair.Value?.ToString())
        .FirstOrDefault();
}

public sealed class LogCaptureProvider : ILoggerProvider, ISupportExternalScope
{
    private readonly ConcurrentQueue<CapturedLogEntry> _entries = new();
    private IExternalScopeProvider _scopeProvider = new LoggerExternalScopeProvider();

    public IReadOnlyCollection<CapturedLogEntry> Entries => _entries.ToArray();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

    public void SetScopeProvider(IExternalScopeProvider scopeProvider) => _scopeProvider = scopeProvider;

    public void Clear() => _entries.Clear();

    public void Dispose()
    {
    }

    private void Capture(LogLevel level, string category, string message, Exception? exception)
    {
        var scopes = new List<object?>();
        _scopeProvider.ForEachScope((scope, collectedScopes) => collectedScopes.Add(scope), scopes);
        _entries.Enqueue(new CapturedLogEntry(level, category, message, exception?.ToString(), scopes));
    }

    private sealed class CapturingLogger(string category, LogCaptureProvider provider) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull =>
            provider._scopeProvider.Push(state);

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            provider.Capture(logLevel, category, formatter(state, exception), exception);
    }
}

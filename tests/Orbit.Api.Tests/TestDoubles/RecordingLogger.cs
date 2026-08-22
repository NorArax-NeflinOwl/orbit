using Microsoft.Extensions.Logging;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="ILogger{T}"/> stub that records every call instead of writing anywhere, so tests
/// can assert on the exact formatted message, level, and exception a component logged.
/// </summary>
internal sealed class RecordingLogger<T> : ILogger<T>
{
    private readonly List<LogEntry> _entries = [];

    public IReadOnlyList<LogEntry> Entries => _entries;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        _entries.Add(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}

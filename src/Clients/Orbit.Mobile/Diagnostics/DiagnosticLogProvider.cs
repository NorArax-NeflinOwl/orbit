using Microsoft.Extensions.Logging;

namespace Orbit.Mobile.Diagnostics;

/// <summary>
/// How much the app writes down. Warnings and worse by default: a log is only read after something went
/// wrong, and recording everything would push the interesting lines out of a capped file long before
/// anybody asked for it.
/// </summary>
public sealed class DiagnosticLogVerbosity
{
    public LogLevel Minimum { get; private set; } = LogLevel.Warning;

    /// <summary>
    /// Raised while actually chasing something, and deliberately not remembered across launches - a
    /// phone left on Debug fills its log with noise and the reader has no reason to think about it
    /// again once the bug is found.
    /// </summary>
    public bool IsVerbose
    {
        get => Minimum <= LogLevel.Debug;
        set => Minimum = value ? LogLevel.Debug : LogLevel.Warning;
    }
}

/// <summary>
/// Puts everything the app already logs into <see cref="DiagnosticLogFile"/>.
///
/// A logging provider rather than a thing screens call: every part of Orbit.Mobile already writes
/// through ILogger, so there is nothing to change anywhere else, and a log statement added later is
/// captured without anybody remembering to do so.
/// </summary>
public sealed class DiagnosticLogProvider : ILoggerProvider
{
    private readonly DiagnosticLogFile _file;
    private readonly DiagnosticLogVerbosity _verbosity;

    public DiagnosticLogProvider(DiagnosticLogFile file, DiagnosticLogVerbosity verbosity)
    {
        _file = file;
        _verbosity = verbosity;
    }

    public ILogger CreateLogger(string categoryName) => new DiagnosticLogWriter(_file, _verbosity, categoryName);

    public void Dispose()
    {
    }

    private sealed class DiagnosticLogWriter : ILogger
    {
        private readonly DiagnosticLogFile _file;
        private readonly DiagnosticLogVerbosity _verbosity;
        private readonly string _categoryName;

        public DiagnosticLogWriter(DiagnosticLogFile file, DiagnosticLogVerbosity verbosity, string categoryName)
        {
            _file = file;
            _verbosity = verbosity;
            _categoryName = ShortNameOf(categoryName);
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= _verbosity.Minimum && logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = $"{_categoryName}: {formatter(state, exception)}";
            if (exception is not null)
            {
                // On its own line, which is what makes it a continuation the server's parser attaches to
                // this entry rather than a new one.
                message += Environment.NewLine + exception;
            }

            _file.Append(Describe(logLevel), message);
        }

        /// <summary>The names the server's parser expects, which are ILogger's own spellings.</summary>
        private static string Describe(LogLevel logLevel) => logLevel switch
        {
            LogLevel.Trace => "Trace",
            LogLevel.Debug => "Debug",
            LogLevel.Information => "Information",
            LogLevel.Warning => "Warning",
            LogLevel.Error => "Error",
            _ => "Critical"
        };

        /// <summary>
        /// The class rather than its whole namespace. A phone log is read on a screen, and
        /// "Orbit.Mobile.Sync.ChatSynchronizer" spends most of a line saying what every line says.
        /// </summary>
        private static string ShortNameOf(string categoryName)
            => categoryName.LastIndexOf('.') is var lastDot && lastDot >= 0
                ? categoryName[(lastDot + 1)..]
                : categoryName;
    }
}

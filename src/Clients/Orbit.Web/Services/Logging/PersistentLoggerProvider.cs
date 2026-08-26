using Microsoft.JSInterop;


namespace Orbit.Web.Services.Logging;

/// <summary>
/// Mirrors every Warning-or-above log line into a small ring buffer kept in the browser's localStorage
/// (see wwwroot/js/clientLogging.js), so a person without access to devtools - most notably on a phone -
/// can retrieve the last few errors via the "Copy error details" link on #blazor-error-ui in
/// index.html, instead of just the generic "An unexpected error occurred" banner telling them nothing.
/// Registered as ILoggerProvider in Program.cs, so the standard logging pipeline picks it up alongside
/// the default browser-console provider - callers keep using plain ILogger&lt;T&gt;, nothing extra to opt into.
/// </summary>
public sealed class PersistentLoggerProvider : ILoggerProvider
{
    private readonly IJSRuntime _jsRuntime;
    private readonly DevicePreferences _devicePreferences;

    public PersistentLoggerProvider(IJSRuntime jsRuntime, DevicePreferences devicePreferences)
    {
        _jsRuntime = jsRuntime;
        _devicePreferences = devicePreferences;
    }

    public ILogger CreateLogger(string categoryName) => new PersistentLogger(categoryName, _jsRuntime, _devicePreferences);

    public void Dispose()
    {
    }

    private sealed class PersistentLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly IJSRuntime _jsRuntime;
        private readonly DevicePreferences _devicePreferences;

        public PersistentLogger(string categoryName, IJSRuntime jsRuntime, DevicePreferences devicePreferences)
        {
            _categoryName = categoryName;
            _jsRuntime = jsRuntime;
            _devicePreferences = devicePreferences;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        // Read on every call rather than captured once: changing the level in Options has to take effect
        // there and then, not on the next reload. Warning is the default for the reason it used to be the
        // only answer - anything lower fills the ring buffer with routine noise long before an actual
        // failure needs the space.
        public bool IsEnabled(LogLevel logLevel) => logLevel >= _devicePreferences.MinimumLogLevel;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            AppendToBrowserStorage(logLevel, formatter(state, exception), exception);
        }

        /// <summary>
        /// Fire-and-forget by design: persisting a diagnostic log line must never throw into, block, or
        /// otherwise affect the code path being logged. "async void" is normally avoided, but Log&lt;TState&gt;
        /// is a synchronous interface method with no Task to return, so this is the standard escape hatch
        /// for that case - the try/catch below is what keeps a JS interop failure from becoming an
        /// unobserved exception.
        /// </summary>
        private async void AppendToBrowserStorage(LogLevel logLevel, string message, Exception? exception)
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync(
                    "OrbitClientLogging.appendLog", logLevel.ToString(), _categoryName, message, exception?.ToString());
            }
            catch
            {
                // Best-effort diagnostics only - swallow so a storage/interop failure never surfaces as
                // its own error.
            }
        }
    }
}

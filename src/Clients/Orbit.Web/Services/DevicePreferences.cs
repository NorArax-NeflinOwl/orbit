using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// The settings that belong to this browser rather than to the account, kept in localStorage the same
/// way ThemeService keeps the theme.
///
/// Per device on purpose. Whether Orbit may ask for a location gates a browser permission, which is
/// itself granted per device and per origin - syncing the answer across devices would mean one of them
/// claiming an answer another one gave. The diagnostics settings are about what this browser reports
/// while someone is looking at it, which is the same kind of thing.
///
/// Takes no ILogger, and must not. PersistentLoggerProvider reads MinimumLogLevel from here on every
/// line it considers, so anything this class logged would be asking the logging pipeline to build
/// itself - which is a dependency cycle at startup (ILoggerProvider -> DevicePreferences -> ILogger&lt;T&gt;
/// -> ILoggerFactory -> ILoggerProvider) and a recursion at runtime. A class the logger depends on
/// cannot log. Both catch blocks below fall back to the documented default instead, which is the whole
/// answer this class has to give.
/// </summary>
public sealed class DevicePreferences
{
    private readonly IJSRuntime _jsRuntime;

    public DevicePreferences(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>Raised after a preference changes, so a page showing the current choice (Options) can refresh.</summary>
    public event Action? Changed;

    /// <summary>
    /// Whether Orbit may ask this browser for the device's position. Off until someone says otherwise:
    /// the browser's own permission prompt is a question worth having agreed to beforehand, and a map
    /// that asks the moment it opens is a map that asked without being invited.
    /// </summary>
    public bool AllowLocation { get; private set; }

    /// <summary>
    /// Debug shows the diagnostics the app can report about itself; Release keeps them out of the way.
    /// The name matches what a developer expects it to mean, and it is a runtime choice rather than the
    /// build's own configuration, which is fixed long before anyone opens Options.
    /// </summary>
    public DiagnosticsMode DiagnosticsMode { get; private set; } = DiagnosticsMode.Release;

    /// <summary>
    /// The least severe line this browser keeps in its own log (see PersistentLoggerProvider). Warning
    /// by default: anything lower fills the ring buffer with routine noise long before an actual
    /// failure needs the space.
    /// </summary>
    public LogLevel MinimumLogLevel { get; private set; } = LogLevel.Warning;

    public async Task InitializeAsync()
    {
        AllowLocation = await ReadAsync(StorageKeys.AllowLocation) == "true";
        DiagnosticsMode = Enum.TryParse<DiagnosticsMode>(await ReadAsync(StorageKeys.DiagnosticsMode), out var mode)
            ? mode
            : DiagnosticsMode.Release;
        MinimumLogLevel = Enum.TryParse<LogLevel>(await ReadAsync(StorageKeys.MinimumLogLevel), out var level)
            ? level
            : LogLevel.Warning;
    }

    public Task SetAllowLocationAsync(bool allowLocation)
    {
        AllowLocation = allowLocation;
        return WriteAsync(StorageKeys.AllowLocation, allowLocation ? "true" : "false");
    }

    public Task SetDiagnosticsModeAsync(DiagnosticsMode mode)
    {
        DiagnosticsMode = mode;
        return WriteAsync(StorageKeys.DiagnosticsMode, mode.ToString());
    }

    public Task SetMinimumLogLevelAsync(LogLevel level)
    {
        MinimumLogLevel = level;
        return WriteAsync(StorageKeys.MinimumLogLevel, level.ToString());
    }

    /// <summary>
    /// Reading a preference must never stop a page loading. A browser with storage blocked outright
    /// (private windows in some browsers, embedded webviews) throws here, and the right answer then is
    /// the default - which for the location is "don't ask", the safe way round.
    /// </summary>
    private async Task<string?> ReadAsync(string key)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    private async Task WriteAsync(string key, string value)
    {
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // The setting still applies for this session - it just won't be remembered next time.
        }
        finally
        {
            Changed?.Invoke();
        }
    }

    private static class StorageKeys
    {
        public const string AllowLocation = "orbit-allow-location";
        public const string DiagnosticsMode = "orbit-diagnostics-mode";
        public const string MinimumLogLevel = "orbit-minimum-log-level";
    }
}

/// <summary>How much the app reports about itself while someone is using it.</summary>
public enum DiagnosticsMode
{
    /// <summary>The ordinary way to run: nothing about Orbit's internals on screen.</summary>
    Release,

    /// <summary>Shows what the app can tell you about itself - the captured client log, and detail behind an error rather than just "something went wrong".</summary>
    Debug
}

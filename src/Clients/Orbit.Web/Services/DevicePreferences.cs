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
    /// Whether the links that hand something off to Google - a day in Google Calendar, a place in Google
    /// Maps - are offered at all. On unless somebody says otherwise: they are shortcuts, and an account
    /// that qualifies for them was not asked for anything in order to.
    ///
    /// Per device like the rest of this class, because it is about what this browser puts in front of
    /// whoever is at it. Turning it off does not disconnect a Google account or change anything on the
    /// server - see GoogleIntegrationAccess, and the Google row in Options for the account-level link.
    /// </summary>
    public bool AllowGoogleExtras { get; private set; } = true;

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

    /// <summary>
    /// Whether an account holding the Debugger permission sees Orbit's adverts on this browser. Off until
    /// somebody turns it on: whoever holds that permission is working on Orbit rather than reading it, and
    /// the adverts were getting in the way of exactly that. It is a switch rather than a rule so the
    /// adverts can still be looked at by the people who make them.
    ///
    /// Asked about nobody else - an account without the permission sees adverts whatever this says. See
    /// AdAudience, which is the one place that combines the two.
    /// </summary>
    public bool AllowAdsForDebugger { get; private set; }

    /// <summary>The kinds of task entry there are, in the order the editor's kind picker lists them.</summary>
    public static readonly IReadOnlyList<string> EntryKinds =
    [
        nameof(Orbit.Core.Tasks.TaskItemKind.Checklist),
        nameof(Orbit.Core.Tasks.TaskItemKind.Calendar),
        nameof(Orbit.Core.Tasks.TaskItemKind.Location),
        nameof(Orbit.Core.Tasks.TaskItemKind.Inventory)
    ];

    /// <summary>
    /// Which kinds of task entry a picked name suggestion fills in and makes the same thing as what it
    /// names - see Orbit.Core.Tasks.TaskItem.ReferencesTaskItemId. Every kind until somebody says
    /// otherwise, which is the user's rule; a kind left out still takes the name's words and nothing else.
    /// Per device like the rest of this class, and chosen on Options' Preferences tab.
    /// </summary>
    public IReadOnlySet<string> KindsFilledFromSuggestions { get; private set; } = new HashSet<string>(EntryKinds);

    public async Task InitializeAsync()
    {
        // Nothing stored means every kind; an empty string is somebody having switched every one off.
        KindsFilledFromSuggestions = await ReadAsync(StorageKeys.KindsFilledFromSuggestions) is { } storedKinds
            ? storedKinds.Split(',', StringSplitOptions.RemoveEmptyEntries).Where(EntryKinds.Contains).ToHashSet()
            : new HashSet<string>(EntryKinds);
        AllowLocation = await ReadAsync(StorageKeys.AllowLocation) == "true";
        // Anything but an explicit "false" leaves them on: a browser that has never been asked, and one
        // whose storage cannot be read at all, both mean "nobody turned these off".
        AllowGoogleExtras = await ReadAsync(StorageKeys.AllowGoogleExtras) != "false";
        DiagnosticsMode = Enum.TryParse<DiagnosticsMode>(await ReadAsync(StorageKeys.DiagnosticsMode), out var mode)
            ? mode
            : DiagnosticsMode.Release;
        MinimumLogLevel = Enum.TryParse<LogLevel>(await ReadAsync(StorageKeys.MinimumLogLevel), out var level)
            ? level
            : LogLevel.Warning;
        // Only an explicit "true" turns them on, the same way round as the location: a browser that has
        // never been asked, and one whose storage cannot be read, both mean "nobody asked for them".
        AllowAdsForDebugger = await ReadAsync(StorageKeys.AllowAdsForDebugger) == "true";
    }

    public Task SetAllowLocationAsync(bool allowLocation)
    {
        AllowLocation = allowLocation;
        return WriteAsync(StorageKeys.AllowLocation, allowLocation ? "true" : "false");
    }

    public Task SetAllowGoogleExtrasAsync(bool allowGoogleExtras)
    {
        AllowGoogleExtras = allowGoogleExtras;
        return WriteAsync(StorageKeys.AllowGoogleExtras, allowGoogleExtras ? "true" : "false");
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

    public Task SetAllowAdsForDebuggerAsync(bool allowAds)
    {
        AllowAdsForDebugger = allowAds;
        return WriteAsync(StorageKeys.AllowAdsForDebugger, allowAds ? "true" : "false");
    }

    /// <summary>Switches one kind of entry in or out of <see cref="KindsFilledFromSuggestions"/>.</summary>
    public Task SetKindFilledFromSuggestionsAsync(string kind, bool isFilled)
    {
        var kinds = new HashSet<string>(KindsFilledFromSuggestions);
        if (isFilled)
        {
            kinds.Add(kind);
        }
        else
        {
            kinds.Remove(kind);
        }

        KindsFilledFromSuggestions = kinds;
        return WriteAsync(StorageKeys.KindsFilledFromSuggestions, string.Join(',', EntryKinds.Where(kinds.Contains)));
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
        public const string AllowGoogleExtras = "orbit-allow-google-extras";
        public const string DiagnosticsMode = "orbit-diagnostics-mode";
        public const string MinimumLogLevel = "orbit-minimum-log-level";
        public const string AllowAdsForDebugger = "orbit-allow-ads-for-debugger";
        public const string KindsFilledFromSuggestions = "orbit-kinds-filled-from-suggestions";
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

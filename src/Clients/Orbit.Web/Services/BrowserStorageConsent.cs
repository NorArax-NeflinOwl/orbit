using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>What this browser is allowed to keep, and how much of it there is.</summary>
/// <param name="Preferences">The theme, the accent, what is pinned, how each list is sorted.</param>
/// <param name="Diagnostics">The errors Orbit noticed on this device.</param>
public readonly record struct StorageConsent(bool Preferences, bool Diagnostics);

/// <summary>How many of Orbit's own keys this browser is holding in each category, right now.</summary>
public readonly record struct StoredKeyCounts(int Necessary, int Preferences, int Diagnostics);

/// <summary>
/// The reader's answer to "what may this browser keep", as the Manage cookies dialog asks it.
///
/// Orbit sets no cookies - everything it remembers about a reader is in localStorage - so the footer
/// link is about that. The rule itself lives in wwwroot/js/storageConsent.js rather than here, because
/// it has to be in force before Blazor has started: the token store and the theme script both write
/// during startup, and a gate that only closes once C# is running is a gate with a hole in it. This
/// class is the way the dialog reads and sets it.
/// </summary>
public sealed class BrowserStorageConsent(IJSRuntime jsRuntime)
{
    /// <summary>
    /// Everything is allowed if the answer cannot be read at all - a browser with storage blocked
    /// outright, or the pre-rendered pass with no window. Nothing can be written there either, so the
    /// permissive reading costs nothing and keeps the dialog from claiming a refusal nobody made.
    /// </summary>
    public async Task<StorageConsent> ReadAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<StorageConsent>("OrbitStorageConsent.get");
        }
        catch (JSException)
        {
            return new StorageConsent(Preferences: true, Diagnostics: true);
        }
    }

    /// <summary>
    /// Records the choice. A category turned off is cleared on the spot - see storageConsent.js - so
    /// "off" never means "off from now on, and yesterday's is still there".
    /// </summary>
    public async Task SaveAsync(StorageConsent consent)
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("OrbitStorageConsent.set", consent.Preferences, consent.Diagnostics);
        }
        catch (JSException)
        {
            // Nothing was stored, so nothing needs forgetting.
        }
    }

    /// <summary>
    /// What is actually being held, so the dialog can say "3 things" rather than only naming what could
    /// be there. Zero everywhere when storage cannot be read, which is the truth in that browser.
    /// </summary>
    public async Task<StoredKeyCounts> CountAsync()
    {
        try
        {
            return await jsRuntime.InvokeAsync<StoredKeyCounts>("OrbitStorageConsent.counts");
        }
        catch (JSException)
        {
            return new StoredKeyCounts(0, 0, 0);
        }
    }
}

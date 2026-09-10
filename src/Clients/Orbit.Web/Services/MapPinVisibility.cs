using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Which groups of pins the map draws. The panel down the left lists two kinds of thing - the people
/// sharing a position, and what the reader has planned - and until now the only way to clear either off
/// the map was to stop the shares or delete the plans.
///
/// Kept on the device, like PanelPreferences and the Tasks page's arrangement: it is how one person
/// reads one map on one screen, not something an account carries between them.
///
/// It remembers "show places already past" as well, which is a question about time rather than about a
/// group of pins - kept apart in what it *means*, and together in where it is kept: both are how one
/// person reads one map on one screen. The eye still wins, and the past filter narrows what the eye has
/// already let through. Somebody who hid their plans and then asked to see past ones meant to be shown
/// nothing, not to have the whole lot come back.
/// </summary>
public sealed class MapPinVisibility
{
    /// <summary>
    /// The two lists that have an eye on them. Named rather than passed as strings, so a typo is a
    /// compiler error instead of a preference that silently never comes back.
    /// </summary>
    public enum PinGroup
    {
        /// <summary>Everyone sharing a position with this reader.</summary>
        SharedWithYou,

        /// <summary>Everything in this reader's calendar and lists that says where it happens.</summary>
        YourPlans,

        /// <summary>The places this reader keeps for their own sake - see Orbit.Core.Places.Place.</summary>
        YourPlaces
    }

    private readonly IJSRuntime _jsRuntime;
    private readonly Dictionary<PinGroup, bool> _shown = [];

    public MapPinVisibility(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>
    /// Shown unless somebody said otherwise. Anything but an explicit "false" leaves a group on: a
    /// browser that has never been asked, and one whose storage cannot be read at all, both mean
    /// "nobody hid these" - and a map that opens empty looks broken rather than tidy.
    /// </summary>
    public bool IsShown(PinGroup group) => _shown.GetValueOrDefault(group, true);

    /// <summary>
    /// Whether what is already behind the reader is drawn as well. Off unless they said otherwise - a
    /// map is mostly about where somebody is going, and a screen full of pins for places nobody is going
    /// to again is the last thing a page about that should open as.
    /// </summary>
    public bool ShowsPastPlaces { get; private set; }

    /// <summary>
    /// The day the past is shown from, while it is shown at all. Null is "all of it", which is what the
    /// option meant before there was anywhere to say otherwise.
    /// </summary>
    public DateTime? PastPlacesFrom { get; private set; }

    public async Task InitializeAsync()
    {
        foreach (var group in Enum.GetValues<PinGroup>())
        {
            _shown[group] = await ReadAsync(group) != "false";
        }

        ShowsPastPlaces = await ReadAsync(PastKey) == "true";
        // A day that will not parse is read as "all of it", which is the answer a browser that has
        // never been asked gives - a stored value nobody can read must not leave the option stuck.
        PastPlacesFrom = DateTime.TryParse(await ReadAsync(PastFromKey), out var from) ? from : null;
    }

    /// <summary>
    /// Remembers whether the past is being shown, and from when. Both together, because they are one
    /// answer with two halves: a day means nothing while the past is hidden.
    /// </summary>
    public async Task SetPastPlacesAsync(bool isShown, DateTime? from)
    {
        ShowsPastPlaces = isShown;
        PastPlacesFrom = from;
        await WriteAsync(PastKey, isShown ? "true" : "false");
        await WriteAsync(PastFromKey, from?.ToString("yyyy-MM-dd"));
    }

    public async Task SetShownAsync(PinGroup group, bool isShown)
    {
        _shown[group] = isShown;
        await WriteAsync(StorageKey(group), isShown ? "true" : "false");
    }

    /// <summary>Null clears the entry, which is how "nothing was said" is stored rather than as a word meaning it.</summary>
    private async Task WriteAsync(string key, string? value)
    {
        try
        {
            if (value is null)
            {
                await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", key);
                return;
            }

            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // It still applies for this session - it just won't be remembered for the next one.
        }
    }

    /// <summary>Mirrors PanelPreferences: a browser with storage blocked outright throws here, and the right answer then is the default.</summary>
    private Task<string?> ReadAsync(PinGroup group) => ReadAsync(StorageKey(group));

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

    private static string StorageKey(PinGroup group) => $"orbit-map-pins-{group}";

    private const string PastKey = "orbit-map-past";

    private const string PastFromKey = "orbit-map-past-from";
}

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
/// Deliberately separate from "show places already past", which is a question about time. Somebody who
/// hid their plans and then asked to see past ones meant to be shown nothing, not to have the whole lot
/// come back - so the eye wins, and the past filter narrows what the eye has already let through.
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
        YourPlans
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

    public async Task InitializeAsync()
    {
        foreach (var group in Enum.GetValues<PinGroup>())
        {
            _shown[group] = await ReadAsync(group) != "false";
        }
    }

    public async Task SetShownAsync(PinGroup group, bool isShown)
    {
        _shown[group] = isShown;
        try
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", StorageKey(group), isShown ? "true" : "false");
        }
        catch (JSException)
        {
            // It still applies for this session - it just won't be remembered for the next one.
        }
    }

    /// <summary>Mirrors PanelPreferences: a browser with storage blocked outright throws here, and the right answer then is the default.</summary>
    private async Task<string?> ReadAsync(PinGroup group)
    {
        try
        {
            return await _jsRuntime.InvokeAsync<string?>("localStorage.getItem", StorageKey(group));
        }
        catch (JSException)
        {
            return null;
        }
    }

    private static string StorageKey(PinGroup group) => $"orbit-map-pins-{group}";
}

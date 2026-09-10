using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Which of the map panel's lists this reader keeps at the top of it.
///
/// The panel holds three: who they are sharing their position with, who is sharing one with them, and
/// everything in their calendar and lists that says where it happens. Which of those matters is a
/// question about the day rather than about Orbit - somebody meeting a person wants the names, somebody
/// on their way somewhere wants the plans - and the panel is tall enough on a phone that the third one
/// is a scroll away.
///
/// Kept on the device, like MapPinVisibility beside it and for the same reason: it is how one person
/// reads one map on one screen, not something an account carries between them. Several may be pinned at
/// once; they keep the order the page writes them in, so pinning is "bring this up" rather than "make
/// this first" - the same promise a pinned note or card makes on every other page.
/// </summary>
public sealed class MapPanelPins
{
    /// <summary>
    /// The lists that can be pinned. Named rather than passed as strings, so a typo is a compiler error
    /// instead of a pin that silently never comes back - the same reason MapPinVisibility names its own.
    /// </summary>
    public enum PanelList
    {
        /// <summary>Everyone this reader is sharing their position with.</summary>
        YouAreSharingWith,

        /// <summary>Everyone sharing a position with this reader.</summary>
        SharingWithYou,

        /// <summary>Everything in their calendar and on their lists that says where it happens.</summary>
        YourPlans
    }

    private readonly IJSRuntime _jsRuntime;
    private readonly HashSet<PanelList> _pinned = [];

    public MapPanelPins(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    /// <summary>Nothing is pinned until somebody says so, which leaves the panel in the order it is written.</summary>
    public bool IsPinned(PanelList list) => _pinned.Contains(list);

    public async Task InitializeAsync()
    {
        foreach (var list in Enum.GetValues<PanelList>())
        {
            if (await ReadAsync(StorageKey(list)) == "true")
            {
                _pinned.Add(list);
            }
        }
    }

    public async Task SetPinnedAsync(PanelList list, bool isPinned)
    {
        if (isPinned)
        {
            _pinned.Add(list);
        }
        else
        {
            _pinned.Remove(list);
        }

        await WriteAsync(StorageKey(list), isPinned ? "true" : "false");
    }

    /// <summary>Mirrors MapPinVisibility: a browser with storage blocked outright throws, and the answer then is the default.</summary>
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
            // It still applies for this session - it just won't be remembered for the next one.
        }
    }

    private static string StorageKey(PanelList list) => $"orbit-map-panel-pin-{list}";
}

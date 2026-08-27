using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Which parts of the dashboard this reader has put away (see wwwroot/js/dashboardCards.js). Kept beside
/// <see cref="DashboardPinService"/> rather than inside it: pinning says what matters most, hiding says
/// what is not wanted at all, and a card can be neither. Like pinning, it is not stored server-side -
/// it describes one page on one device and says nothing about what the cards are showing.
///
/// Hidden rather than visible keys are stored, so a card added to the dashboard later shows up by
/// default instead of being invisible to everybody who saved a layout before it existed.
/// </summary>
public sealed class DashboardCardVisibility(IJSRuntime jsRuntime)
{
    private HashSet<string> _hiddenCardKeys = [];

    public async Task InitializeAsync()
    {
        await using var module = await ImportModuleAsync();
        var stored = await module.InvokeAsync<string[]>("getHiddenCards");
        _hiddenCardKeys = [.. stored];
    }

    public bool IsVisible(string cardKey) => !_hiddenCardKeys.Contains(cardKey);

    /// <summary>Whether every part of the dashboard has been put away, which needs saying on the page.</summary>
    public bool IsAnythingVisible(IEnumerable<string> cardKeys) => cardKeys.Any(IsVisible);

    public async Task SetVisibleAsync(string cardKey, bool isVisible)
    {
        if (isVisible)
        {
            _hiddenCardKeys.Remove(cardKey);
        }
        else
        {
            _hiddenCardKeys.Add(cardKey);
        }

        await using var module = await ImportModuleAsync();
        await module.InvokeVoidAsync("setHiddenCards", _hiddenCardKeys);
    }

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/dashboardCards.js");
}

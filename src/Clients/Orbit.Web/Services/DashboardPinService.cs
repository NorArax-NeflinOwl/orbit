using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// Remembers which dashboard cards the reader has pinned to the top of the page (see
/// wwwroot/js/dashboardPins.js). Unlike a pinned note or task list, this is not stored server-side: it
/// describes the layout of one page on one device, which is the same category as the theme preference,
/// and it says nothing about the notes, lists or people the cards are showing.
/// </summary>
public sealed class DashboardPinService(IJSRuntime jsRuntime)
{
    private HashSet<string> _pinnedCardKeys = [];

    public async Task InitializeAsync()
    {
        await using var module = await ImportModuleAsync();
        var stored = await module.InvokeAsync<string[]>("getPinnedCards");
        _pinnedCardKeys = [.. stored];
    }

    public bool IsPinned(string cardKey) => _pinnedCardKeys.Contains(cardKey);

    public async Task SetPinnedAsync(string cardKey, bool isPinned)
    {
        if (isPinned)
        {
            _pinnedCardKeys.Add(cardKey);
        }
        else
        {
            _pinnedCardKeys.Remove(cardKey);
        }

        await using var module = await ImportModuleAsync();
        await module.InvokeVoidAsync("setPinnedCards", _pinnedCardKeys);
    }

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/dashboardPins.js");
}

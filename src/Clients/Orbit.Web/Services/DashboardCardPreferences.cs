using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>What one dashboard card is showing of what it could show.</summary>
public enum DashboardCardFilter
{
    All,

    /// <summary>Only what the reader has pinned - offered where the card's items can be pinned.</summary>
    Pinned,
    HighPriority,
    NormalPriority,
    LowPriority
}

/// <summary>
/// How this reader wants the dashboard's cards shown: which are put away entirely, and what each of the
/// rest is filtered down to. Kept beside <see cref="DashboardPinService"/> rather than inside it -
/// pinning says what matters most, this says what to show at all - and stored the same way: on the
/// device (see wwwroot/js/dashboardCards.js), because it describes one page for one reader and says
/// nothing about what the cards hold.
///
/// Hidden rather than visible keys are stored, so a card added to the dashboard later shows up by
/// default instead of being invisible to everybody who saved a layout before it existed.
/// </summary>
public sealed class DashboardCardPreferences(IJSRuntime jsRuntime)
{
    private HashSet<string> _hiddenCardKeys = [];
    private Dictionary<string, DashboardCardFilter> _filterByCardKey = [];
    private HashSet<Guid> _hiddenFolderIds = [];

    public async Task InitializeAsync()
    {
        await using var module = await ImportModuleAsync();
        _hiddenCardKeys = [.. await module.InvokeAsync<string[]>("getHiddenCards")];
        _hiddenFolderIds =
        [
            .. (await module.InvokeAsync<string[]>("getHiddenFolders"))
                .Select(id => Guid.TryParse(id, out var folderId) ? folderId : (Guid?)null)
                .OfType<Guid>()
        ];
        var storedFilters = await module.InvokeAsync<Dictionary<string, string>>("getCardFilters");
        _filterByCardKey = storedFilters
            .Where(stored => Enum.TryParse<DashboardCardFilter>(stored.Value, out _))
            .ToDictionary(stored => stored.Key, stored => Enum.Parse<DashboardCardFilter>(stored.Value));
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

    /// <summary>
    /// Whether a folder somebody made is drawn on the dashboard at all. Shown unless they said
    /// otherwise: a tab that had to be turned on would be a tab nobody found.
    ///
    /// It hides the tab rather than the folder - the folder is still there on the page it belongs to,
    /// with everything in it - and the dashboard shows what is under the tab that is open, so a folder
    /// with no tab here is one the dashboard stops drawing.
    /// </summary>
    public bool IsFolderShown(Guid folderId) => !_hiddenFolderIds.Contains(folderId);

    public async Task SetFolderShownAsync(Guid folderId, bool isShown)
    {
        if (isShown)
        {
            _hiddenFolderIds.Remove(folderId);
        }
        else
        {
            _hiddenFolderIds.Add(folderId);
        }

        await using var module = await ImportModuleAsync();
        await module.InvokeVoidAsync("setHiddenFolders", _hiddenFolderIds.Select(id => id.ToString()));
    }

    /// <summary>What this card is filtered to - everything, unless the reader has said otherwise.</summary>
    public DashboardCardFilter FilterFor(string cardKey)
        => _filterByCardKey.GetValueOrDefault(cardKey, DashboardCardFilter.All);

    public async Task SetFilterAsync(string cardKey, DashboardCardFilter filter)
    {
        if (filter == DashboardCardFilter.All)
        {
            // Nothing to remember about a card showing everything, which is what a card does by default.
            _filterByCardKey.Remove(cardKey);
        }
        else
        {
            _filterByCardKey[cardKey] = filter;
        }

        await using var module = await ImportModuleAsync();
        await module.InvokeVoidAsync(
            "setCardFilters", _filterByCardKey.ToDictionary(entry => entry.Key, entry => entry.Value.ToString()));
    }

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/dashboardCards.js");
}

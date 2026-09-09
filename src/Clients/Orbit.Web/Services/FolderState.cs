using Orbit.Contracts.Folders;
using Orbit.Core.Folders;

namespace Orbit.Web.Services;

/// <summary>
/// The folders and, for each page made of cards, the tab that is open on it. One state rather than one
/// per page because the folders themselves are read once for the whole visit - but the tab is now the
/// page's own: a folder belongs to one page (see FolderScope), so "Work" on the notes and "Work" on the
/// task lists are two folders, and a single chosen tab could only ever have been right on one of them.
///
/// Mirrors NotificationFeedState's shape - scoped state plus a Changed event that pages re-render on.
/// </summary>
public sealed class FolderState
{
    private readonly FoldersApiClient _foldersApiClient;
    private IReadOnlyList<FolderDto> _folders = [];
    private bool _hasLoaded;

    /// <summary>
    /// The tab open on each page, filled in as pages are visited. Missing means Public - see
    /// <see cref="ChosenOn"/>, which is where that default lives rather than in a constructor that
    /// would have to name every page.
    /// </summary>
    private readonly Dictionary<FolderPage, FolderKey> _chosenByPage = [];

    public FolderState(FoldersApiClient foldersApiClient)
    {
        _foldersApiClient = foldersApiClient;
    }

    /// <summary>Raised when the folders or a chosen tab change, so subscribed pages re-filter their cards.</summary>
    public event Action? Changed;

    /// <summary>Everything somebody made, whatever page it is a tab on - see <see cref="FoldersOn"/> for one page's own.</summary>
    public IReadOnlyList<FolderDto> Folders => _folders;

    /// <summary>The tabs this page draws, oldest first - which is the order they are drawn in.</summary>
    public IReadOnlyList<FolderDto> FoldersOn(FolderPage page)
    {
        var scopes = page.ScopesOn();
        return [.. _folders.Where(folder => scopes.Contains(ScopeOf(folder)))];
    }

    /// <summary>
    /// The ids of the folders this page has tabs for, which is what tells a card's own folder from one
    /// belonging to another page. A note filed under a notes folder read on the task lists is a stale
    /// id as far as that page is concerned, and falls back to a built-in tab - see FolderPlacement.
    /// </summary>
    public IReadOnlyCollection<Guid> KnownFolderIdsOn(FolderPage page)
        => [.. FoldersOn(page).Select(folder => folder.Id)];

    /// <summary>The tab open on this page. Public until somebody presses another one - see FolderKey.Default.</summary>
    public FolderKey ChosenOn(FolderPage page)
        => _chosenByPage.TryGetValue(page, out var chosen) ? chosen : FolderKey.Default;

    /// <summary>
    /// Reads the folders once per visit. Pages call it on every open and only the first does anything,
    /// the same way UserPermissionState.EnsureLoadedAsync works: three pages share this, and each of
    /// them asking on arrival would be three requests for an answer that has not changed.
    /// </summary>
    public async Task EnsureLoadedAsync(CancellationToken cancellationToken = default)
    {
        if (_hasLoaded)
        {
            return;
        }

        _hasLoaded = true;
        await RefreshAsync(cancellationToken);
    }

    public async Task RefreshAsync(CancellationToken cancellationToken = default)
    {
        _folders = await _foldersApiClient.GetFoldersAsync(cancellationToken);
        // The tab somebody was reading may have been deleted from another device. Falling back beats
        // leaving a page filtered to a folder that no longer exists, which shows nothing at all and
        // says nothing about why.
        var stored = _folders.Select(folder => folder.Id).ToHashSet();
        foreach (var page in _chosenByPage.Keys.ToList())
        {
            if (_chosenByPage[page].FolderId is { } chosenId && !stored.Contains(chosenId))
            {
                _chosenByPage[page] = FolderKey.Default;
            }
        }

        Changed?.Invoke();
    }

    public void Choose(FolderPage page, FolderKey folder)
    {
        if (ChosenOn(page) == folder)
        {
            return;
        }

        _chosenByPage[page] = folder;
        Changed?.Invoke();
    }

    /// <summary>
    /// The new folder, already chosen - making one is how somebody says where they want to be. The page
    /// it is made on decides what it is a tab on, and there is no making one where nothing could be
    /// filed into it: see FolderPages.MakesFoldersIn, which is what the dashboard answers null to.
    /// </summary>
    public async Task<FolderDto?> CreateAsync(FolderPage page, string name, CancellationToken cancellationToken = default)
    {
        if (page.MakesFoldersIn() is not { } scope)
        {
            return null;
        }

        var folder = await _foldersApiClient.CreateFolderAsync(name, scope, cancellationToken);
        if (folder is null)
        {
            return null;
        }

        await RefreshAsync(cancellationToken);
        Choose(page, FolderKey.Of(folder.Id));
        return folder;
    }

    public async Task RenameAsync(Guid id, string name, CancellationToken cancellationToken = default)
    {
        await _foldersApiClient.RenameFolderAsync(id, name, cancellationToken);
        await RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Removes the tab. What was in it is not deleted - it goes back to the built-in folder its own
    /// privacy decides (see IFolderRepository.DeleteAsync) - so the reader is put back on Public, where
    /// most of it will now be.
    /// </summary>
    public async Task DeleteAsync(FolderPage page, Guid id, CancellationToken cancellationToken = default)
    {
        await _foldersApiClient.DeleteFolderAsync(id, cancellationToken);
        _chosenByPage[page] = FolderKey.Default;
        await RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Whether a card belongs under the tab open on this page - the question every page made of cards
    /// asks of every card it holds. See FolderPlacement for the rule itself.
    /// </summary>
    public bool ShowsUnderTheChosenTab(FolderPage page, Guid? folderId, bool isPrivate, bool isFinished = false)
        => PlacementOn(page, folderId, isPrivate, isFinished) == ChosenOn(page);

    /// <summary>
    /// Which of this page's tabs a card is under. The page is asked rather than told whether the card is
    /// finished, so a page with no Finished tab never files anything there - see FolderPages.HasAFinishedTab.
    /// </summary>
    public FolderKey PlacementOn(FolderPage page, Guid? folderId, bool isPrivate, bool isFinished = false)
        => FolderPlacement.Of(
            folderId, isPrivate, isFinished && page.HasAFinishedTab(), KnownFolderIdsOn(page));

    /// <summary>What to call a folder on its tab, given the name a built-in one goes by in the reader's language.</summary>
    public string NameOf(FolderKey folder, Func<BuiltInFolder, string> builtInName)
        => folder.BuiltIn is { } builtIn
            ? builtInName(builtIn)
            : _folders.FirstOrDefault(stored => stored.Id == folder.FolderId)?.Name ?? string.Empty;

    /// <summary>
    /// Which kind of thing a folder somebody made holds - see FolderScope. Null for an id this browser
    /// has no folder for, which is what a stale one reads as.
    /// </summary>
    public FolderScope? ScopeOf(Guid folderId)
        => _folders.FirstOrDefault(folder => folder.Id == folderId) is { } stored ? ScopeOf(stored) : null;

    /// <summary>A scope this browser doesn't recognise reads as Tasks - the same fallback the server applies.</summary>
    private static FolderScope ScopeOf(FolderDto folder)
        => Enum.TryParse<FolderScope>(folder.Scope, out var scope) ? scope : FolderScope.Tasks;
}

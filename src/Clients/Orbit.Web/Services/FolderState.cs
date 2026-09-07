using Orbit.Contracts.Folders;
using Orbit.Core.Folders;

namespace Orbit.Web.Services;

/// <summary>
/// The folders and the tab that is open, shared by every page made of cards - the dashboard, the notes
/// and the task lists. One state rather than one per page, because a folder is a place rather than a
/// view setting: somebody reading their "Work" tab and stepping from the notes to the task lists is
/// still in Work, and having to choose it again on each page would make three tabs of the same name
/// behave like three different things.
///
/// Mirrors NotificationFeedState's shape - scoped state plus a Changed event that pages re-render on.
/// </summary>
public sealed class FolderState
{
    private readonly FoldersApiClient _foldersApiClient;
    private IReadOnlyList<FolderDto> _folders = [];
    private bool _hasLoaded;

    public FolderState(FoldersApiClient foldersApiClient)
    {
        _foldersApiClient = foldersApiClient;
    }

    /// <summary>Raised when the folders or the chosen tab change, so subscribed pages re-filter their cards.</summary>
    public event Action? Changed;

    /// <summary>What somebody made, oldest first - the order their tabs are drawn in.</summary>
    public IReadOnlyList<FolderDto> Folders => _folders;

    /// <summary>The ids of the folders that exist, which is what tells a card's own folder from a stale one.</summary>
    public IReadOnlyCollection<Guid> KnownFolderIds { get; private set; } = [];

    /// <summary>The tab that is open. Public until somebody presses another one - see FolderKey.Default.</summary>
    public FolderKey Chosen { get; private set; } = FolderKey.Default;

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
        KnownFolderIds = [.. _folders.Select(folder => folder.Id)];
        // The tab somebody was reading may have been deleted from another device. Falling back beats
        // leaving a page filtered to a folder that no longer exists, which shows nothing at all and
        // says nothing about why.
        if (Chosen.FolderId is { } chosenId && !KnownFolderIds.Contains(chosenId))
        {
            Chosen = FolderKey.Default;
        }

        Changed?.Invoke();
    }

    public void Choose(FolderKey folder)
    {
        if (Chosen == folder)
        {
            return;
        }

        Chosen = folder;
        Changed?.Invoke();
    }

    /// <summary>The new folder, already chosen - making one is how somebody says where they want to be.</summary>
    public async Task<FolderDto?> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        var folder = await _foldersApiClient.CreateFolderAsync(name, cancellationToken);
        if (folder is null)
        {
            return null;
        }

        await RefreshAsync(cancellationToken);
        Choose(FolderKey.Of(folder.Id));
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
    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _foldersApiClient.DeleteFolderAsync(id, cancellationToken);
        Chosen = FolderKey.Default;
        await RefreshAsync(cancellationToken);
    }

    /// <summary>
    /// Whether a card belongs under the tab that is open - the question every page made of cards asks
    /// of every card it holds. See FolderPlacement for the rule itself.
    /// </summary>
    public bool ShowsUnderTheChosenTab(Guid? folderId, bool isPrivate, bool isFinished = false)
        => FolderPlacement.Of(folderId, isPrivate, isFinished, KnownFolderIds) == Chosen;

    /// <summary>What to call a folder on its tab, given the name a built-in one goes by in the reader's language.</summary>
    public string NameOf(FolderKey folder, Func<BuiltInFolder, string> builtInName)
        => folder.BuiltIn is { } builtIn
            ? builtInName(builtIn)
            : _folders.FirstOrDefault(stored => stored.Id == folder.FolderId)?.Name ?? string.Empty;
}

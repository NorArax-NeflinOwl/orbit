using Orbit.Core.Text;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Core.Abstractions;
using Orbit.Core.Folders;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Folders;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Security;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>Inventories, read from the local database exactly as the other three features are.</summary>
public sealed partial class InventoryViewModel : ObservableObject
{
    private readonly LocalInventoryRepository _inventories;
    private readonly InventorySynchronizer _synchronizer;
    private readonly INetworkStatus _networkStatus;
    private readonly SyncState _syncState;
    private readonly IScreenNavigator _navigator;
    private readonly Translations _translations;
    private readonly PrivateItemGate _privateItems;

    /// <inheritdoc cref="Notes.NotesViewModel._notifications"/>
    private readonly LocalNotificationRepository? _notifications;

    [ObservableProperty]
    private string _newInventoryName = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    /// <summary>
    /// The inventories on screen, items and all, kept so the search can read them without going back to
    /// the database on every keystroke. Refreshed wherever the rows are.
    /// </summary>
    private IReadOnlyList<LocalInventory> _stored = [];

    /// <summary>
    /// Every shelf on the phone, before the open tab narrows it - see <see cref="_stored"/>, which is
    /// what the rows are drawn from. The search across shelves is asked of this one: "where is the
    /// flour" is a question about the reader's whole inventory, not about the tab they happen to be
    /// standing on, and answering it from one tab would say "it is nowhere" about a shelf filed under
    /// another. The folders narrow the list, not the search.
    /// </summary>
    private IReadOnlyList<LocalInventory> _everyShelf = [];

    /// <summary>
    /// Inventories this device could not look inside - sealed with a key it has not got, or private
    /// while private things are locked. Counted rather than skipped: a search that quietly leaves one
    /// out answers "it is nowhere" when the truth is "I could not look there". Counted rather than
    /// named, because a name is one of the things being kept back.
    /// </summary>
    private int _unsearchableInventoryCount;

    /// <summary>Why the last press could not do what it said - empty while nothing needs saying.</summary>
    [ObservableProperty]
    private string _message = string.Empty;

    public InventoryViewModel(
        LocalInventoryRepository inventories, InventorySynchronizer synchronizer, INetworkStatus networkStatus,
        PrivateItemGate privateItems, SyncState syncState, IScreenNavigator navigator, Translations translations,
        SharePanel share,
        LocalFolderRepository folders, IChosenFolderStore chosenFolder, FolderSynchronizer folderSynchronizer,
        SharingSeveral? sharingSeveral = null, LocalNotificationRepository? notifications = null)
    {
        _notifications = notifications;
        Picking = new PickingSeveral(
            translations,
            new PickingActions(
                SharedItemKind.Inventory,
                (localId, folderId, token) => inventories.FileAsync(localId, folderId, token),
                (localId, isArchived, token) => inventories.ArchiveAsync(localId, isArchived, token),
                async token =>
                {
                    await ShowStoredInventoriesAsync(token);
                    await SynchroniseAsync(token);
                },
                () => Folders!.Made),
            sharingSeveral);
        Picking.Changed += (_, _) => MarkThePicked();
        _folders = folders;
        _folderSynchronizer = folderSynchronizer;
        Folders = new FolderTabs(folders, chosenFolder, translations, FolderPage.Inventories);
        _inventories = inventories;
        _synchronizer = synchronizer;
        _networkStatus = networkStatus;
        _privateItems = privateItems;
        _syncState = syncState;
        _navigator = navigator;
        _translations = translations;
        Share = share;
    }

    /// <summary>
    /// Offering one of these to somebody else, from its own card - see SharePanel, and Orbit.Web's
    /// Inventory card, which opens its share panel in the same place. One panel for the screen rather
    /// than one per card: only one is ever open, and it is told which inventory it is about when it is.
    /// </summary>
    public SharePanel Share { get; }

    /// <summary>Several shelves chosen to be filed, put away or shared together - see PickingSeveral.</summary>
    public PickingSeveral Picking { get; }

    /// <summary>The menu's "Select": starts choosing shelves, or stops.</summary>
    [RelayCommand]
    private void ToggleChoosing()
    {
        if (Picking.IsPicking)
        {
            Picking.Stop();
            return;
        }

        Picking.Start();
    }

    /// <summary>Puts the mark on every row, or takes it off, for what is chosen now - see NotesViewModel.</summary>
    private void MarkThePicked()
    {
        for (var index = 0; index < Inventories.Count; index++)
        {
            var row = Inventories[index];
            var marked = row with { OffersPicking = Picking.IsPicking, IsPicked = Picking.Holds(row.LocalId) };
            if (marked != row)
            {
                Inventories[index] = marked;
            }
        }
    }

    /// <inheritdoc cref="Notes.NotesViewModel.HasMessage"/>
    public bool HasMessage => Message.Length > 0;

    partial void OnMessageChanged(string value) => OnPropertyChanged(nameof(HasMessage));

    private readonly LocalFolderRepository _folders;

    /// <summary>
    /// <inheritdoc cref="Notes.NotesViewModel.Folders" path="/summary/node()"/> Pushed before the
    /// shelves on every sync, for the reason FolderNotOnTheServerYet gives.
    /// </summary>
    private readonly FolderSynchronizer _folderSynchronizer;

    /// <summary>
    /// <inheritdoc cref="Notes.NotesViewModel.Folders" path="/summary/node()"/>
    /// </summary>
    public FolderTabs Folders { get; }

    /// <summary>Those folders as the menu draws them, with how many shelves are in each.</summary>
    public ObservableCollection<FolderChoice> FolderChoices { get; } = [];

    /// <inheritdoc cref="Notes.NotesViewModel.ChosenFolderName"/>
    public string ChosenFolderName
        => FolderChoices.FirstOrDefault(choice => choice.IsChosen)?.Name ?? string.Empty;

    /// <inheritdoc cref="Notes.NotesViewModel.NewFolderName"/>
    [ObservableProperty]
    private string _newFolderName = string.Empty;

    /// <inheritdoc cref="Notes.NotesViewModel.FolderBeingRenamed"/>
    [ObservableProperty]
    private Guid? _folderBeingRenamed;

    /// <inheritdoc cref="Notes.NotesViewModel.FolderRowAction"/>
    public string FolderRowAction => FolderBeingRenamed is null ? _translations["Add"] : _translations["Rename"];

    partial void OnFolderBeingRenamedChanged(Guid? value) => OnPropertyChanged(nameof(FolderRowAction));

    /// <inheritdoc cref="Notes.NotesViewModel.StartRenamingTheOpenFolder"/>
    public void StartRenamingTheOpenFolder()
    {
        if (Folders.Chosen.FolderId is { } folderId)
        {
            FolderBeingRenamed = folderId;
            NewFolderName = ChosenFolderName;
        }
    }

    /// <inheritdoc cref="Notes.NotesViewModel.StartNamingANewFolder"/>
    public void StartNamingANewFolder()
    {
        FolderBeingRenamed = null;
        NewFolderName = string.Empty;
    }

    /// <inheritdoc cref="Notes.NotesViewModel.IsChosenFolderHiddenOnTheDashboard"/>
    public bool IsChosenFolderHiddenOnTheDashboard
        => Folders.Chosen.FolderId is { } folderId && Folders.IsHiddenOnTheDashboard(folderId);

    /// <summary>
    /// Takes the open folder off the dashboard's menu, or puts it back - see FolderTabs.HideOnTheDashboard.
    /// The shelves are cards the dashboard is made of, so their folders are tabs there too, and this is
    /// offered on the page the folder belongs to exactly as it is for the notes and the lists.
    /// </summary>
    [RelayCommand]
    private void ToggleShownOnTheDashboard()
    {
        if (Folders.Chosen.FolderId is { } folderId)
        {
            Folders.HideOnTheDashboard(folderId, !Folders.IsHiddenOnTheDashboard(folderId));
            OnPropertyChanged(nameof(IsChosenFolderHiddenOnTheDashboard));
        }
    }

    /// <summary>Reading the screen under another folder - chosen from the menu under its name.</summary>
    [RelayCommand]
    private async Task ChooseFolderAsync(FolderKey key, CancellationToken cancellationToken)
    {
        Folders.Choose(key);
        await ShowStoredInventoriesAsync(cancellationToken);
    }

    /// <summary>
    /// A new folder, made and shown at once whether or not there is a connection - a folder is a name,
    /// so nothing about it waits on a server. The screen moves to it, because making one is how somebody
    /// says where the next thing goes. The same row renames the open folder - see FolderBeingRenamed.
    /// </summary>
    [RelayCommand]
    private async Task MakeFolderAsync(string? name, CancellationToken cancellationToken)
    {
        if (name?.Trim() is not { Length: > 0 } wanted)
        {
            return;
        }

        if (FolderBeingRenamed is { } renamed)
        {
            await _folders.RenameAsync(renamed, wanted, cancellationToken);
            FolderBeingRenamed = null;
        }
        else
        {
            var folder = await _folders.CreateAsync(wanted, FolderScope.Inventories, cancellationToken);
            Folders.Choose(FolderKey.Of(folder.LocalId));
        }

        NewFolderName = string.Empty;
        await ShowStoredInventoriesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    /// <summary>
    /// Takes the folder being read away and leaves everything that was in it, which is what the server
    /// does too: getting rid of the place is not a decision to get rid of what was in it.
    /// </summary>
    [RelayCommand]
    private async Task DeleteFolderAsync(CancellationToken cancellationToken)
    {
        if (Folders.Chosen.FolderId is not { } folderId)
        {
            return;
        }

        await _folders.DeleteAsync(folderId, cancellationToken);
        Folders.Choose(FolderKey.Default);

        await ShowStoredInventoriesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    public ObservableCollection<InventoryRow> Inventories { get; } = [];


    /// <summary>
    /// What the reader is looking for across every inventory. This page lists shelves and not what is on
    /// them, so where something is was the one question it could not answer.
    /// </summary>
    [ObservableProperty]
    private string _searchedItemName = string.Empty;

    /// <summary>What was found, and on which shelf - see <see cref="InventoryItemMatch"/>.</summary>
    public ObservableCollection<InventoryItemMatch> ItemMatches { get; } = [];

    /// <summary>The list of shelves steps aside while a search is on, since the answer replaces it.</summary>
    public bool IsSearchingItems => SearchedItemName.Trim().Length > 0;

    public bool IsShowingInventories => !IsSearchingItems;

    /// <summary>An empty shelf list and a search that found nothing need different words.</summary>
    public bool FoundNothing => IsSearchingItems && ItemMatches.Count == 0;

    /// <summary>
    /// What was found, and - when an inventory is sealed with a key this phone has not got - that the
    /// answer is short of those. Saying only the count would let "nothing found" stand for "I could not
    /// look there", which is the one answer a search must never give by accident.
    /// </summary>
    public string ItemMatchSummary
        => _unsearchableInventoryCount == 0
            ? _translations.Format("Found in {0} of {1} inventories.", InventoriesMatched, _everyShelf.Count)
            : _translations.Format(
                "Found in {0} of {1} inventories. {2} could not be opened, so nothing in them was searched.",
                InventoriesMatched, _everyShelf.Count, _unsearchableInventoryCount);

    private int InventoriesMatched
        => ItemMatches.Select(match => match.InventoryLocalId).Distinct().Count();

    [RelayCommand]
    private void ClearItemSearch() => SearchedItemName = string.Empty;

    [RelayCommand]
    private void OpenMatch(InventoryItemMatch? match)
    {
        if (match is not null)
        {
            // Opened on the thing that was found: a search across every shelf that landed somebody on a
            // shelf and left them looking for it again would have answered half the question.
            _navigator.ShowInventory(match.InventoryLocalId, match.Item.Item.Id);
        }
    }

    /// <summary>
    /// Answers "which inventory is this in", from what the phone already holds rather than by asking the
    /// server. Every inventory's items came down with the inventory, so there is nothing to fetch and
    /// nothing to cache - and a private inventory keeps no item rows on the server at all, so an
    /// endpoint could not have answered for those anyway.
    ///
    /// Matched anywhere in the name and without case, the same as the shelf's own search box: a shelf
    /// holds "Flour, wheat" and somebody typing "flour" means it.
    /// </summary>
    private void ShowMatchingItems()
    {
        ItemMatches.Clear();
        if (SearchedItemName.Trim() is { Length: > 0 } wanted)
        {
            var found = _everyShelf
                .Where(CanBeSearched)
                .SelectMany(inventory => inventory.Items.Select(item => new InventoryItemMatch(
                    inventory.LocalId, inventory.Name, InventoryItemRow.From(
                        item, _translations,
                        usage: inventory.ItemUsage.GetValueOrDefault(item.Id ?? Guid.Empty)))))
                .Where(match => LooseText.Holds(match.Name, wanted))
                .OrderBy(match => match.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(match => match.InventoryName, StringComparer.CurrentCultureIgnoreCase);

            foreach (var match in found)
            {
                ItemMatches.Add(match);
            }
        }

        OnPropertyChanged(nameof(IsSearchingItems));
        OnPropertyChanged(nameof(IsShowingInventories));
        OnPropertyChanged(nameof(FoundNothing));
        OnPropertyChanged(nameof(ItemMatchSummary));
    }

    partial void OnSearchedItemNameChanged(string value) => ShowMatchingItems();

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await ShowStoredInventoriesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanAddInventory))]
    private async Task AddInventoryAsync(CancellationToken cancellationToken)
    {
        await _inventories.CreateAsync(NewInventoryName.Trim(), cancellationToken);
        NewInventoryName = string.Empty;

        await ShowStoredInventoriesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private bool CanAddInventory => NewInventoryName.Trim().Length > 0;

    /// <inheritdoc cref="Notes.NotesViewModel.Open"/>
    [RelayCommand]
    private void OpenInventory(InventoryRow? row)
    {
        // While choosing, a press anywhere on a row chooses it - see PickingSeveral.
        if (row is not null && Picking.Toggle(row.LocalId))
        {
            return;
        }

        if (row is { CanBeOpened: true })
        {
            _navigator.ShowInventory(row.LocalId);
        }
    }

    /// <summary>
    /// Getting rid of an inventory from its own card, which is what Orbit.Web's Inventory card offers.
    /// Only for one this reader owns; a shared one is somebody else's.
    ///
    /// Asking first is the page's job, not this one's: what a question looks like is a screen's
    /// business, and there is nothing here to ask with.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync(InventoryRow? row, CancellationToken cancellationToken)
    {
        if (row is not { IsSharedWithMe: false })
        {
            return;
        }

        var outcome = await _inventories.DeleteAsync(row.LocalId, cancellationToken);
        if (outcome.WasRefused())
        {
            Message = outcome.Explain(RefusalMessage, _translations);
            return;
        }

        Message = string.Empty;
        await ShowStoredInventoriesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    /// <summary>
    /// Opens the share panel on the inventory the card names. A private one is offered to nobody - the
    /// server holds no readable copy to hand over, which is what makes it private - and one the server
    /// has not seen yet cannot be offered either, since there is no id to share.
    /// </summary>
    [RelayCommand]
    private void OfferToShare(InventoryRow? row)
    {
        if (row is not { CanBeShared: true }
            || _stored.FirstOrDefault(inventory => inventory.LocalId == row.LocalId) is not
                { ServerId: { } serverId } stored)
        {
            return;
        }

        Share.Describes(
            SharedItemKind.Inventory, serverId, stored.Name,
            stored.AccessLevel == "CanEdit" ? null : stored.OwnerUserId);
        Share.IsOpen = true;
        Message = string.Empty;
    }

    /// <summary>
    /// The dictionary key, not the text itself - see <see cref="Translations"/>. The same sentence the
    /// inventory's own screen uses, because it is the same refusal about the same inventory.
    /// </summary>
    private const string RefusalMessage =
        "Somebody else can change this inventory, and Orbit can't be reached to check. "
        + "It stays read-only until you're back online.";

    /// <inheritdoc cref="Notes.NotesViewModel.UnlockPrivateAsync"/>
    [RelayCommand]
    private async Task UnlockPrivateAsync(CancellationToken cancellationToken)
    {
        if (await _privateItems.TryUnlockAsync(cancellationToken))
        {
            ShowRows();
        }
    }

    /// <summary>
    /// Whether a search may look inside. A sealed inventory holds nothing this device could read, and a
    /// private one while private things are locked holds nothing it may show - see PrivateItemGate.
    /// </summary>
    private bool CanBeSearched(LocalInventory inventory)
        => !inventory.IsSealed && (!inventory.IsPrivate || _privateItems.IsUnlocked);

    /// <summary>
    /// Draws the shelves from what is on the phone, asking the server nothing.
    ///
    /// Public for one caller beyond this class: the page calls it when a sync that nobody on this
    /// screen asked for has brought something down, so a screen left open stops showing what it was
    /// shown when it was opened - see PeriodicSync and SyncState.BroughtSomethingNew.
    /// </summary>
    public async Task ShowStoredInventoriesAsync(CancellationToken cancellationToken)
    {
        var held = await _inventories.GetAllAsync(cancellationToken);
        var pending = await _inventories.GetPendingLocalIdsAsync(cancellationToken);
        await Folders.ReadAsync(cancellationToken);

        // Where each shelf is, by the rule both clients share. A shelf is never finished, so the
        // question a task list is asked here is not asked of it - see FolderPlacement.
        var placements = held.ToDictionary(
            inventory => inventory.LocalId,
            inventory => Folders.Where(
                inventory.FolderId, inventory.IsPrivate, isFinished: false, inventory.IsArchived));

        // And which folders hold something the reader has not seen - a warning about something going
        // off, say - so the menu can say which one to open. See UnreadNews, and the dot the browser puts
        // on the tab this entry stands for.
        var unread = _notifications is null
            ? []
            : UnreadNews.AddressesIn(await _notifications.GetUnreadAsync(cancellationToken));

        FolderChoices.Clear();
        foreach (var choice in Folders.Describe(
            [.. held.Select(inventory => new RowInAFolder(
                placements[inventory.LocalId],
                inventory.ServerId is { } serverId && UnreadNews.About(unread, $"/inventory/{serverId}")))]))
        {
            FolderChoices.Add(choice);
        }

        OnPropertyChanged(nameof(ChosenFolderName));

        _everyShelf = held;
        _stored = [.. held.Where(inventory => Folders.Holds(placements[inventory.LocalId]))];
        _pending = pending;
        OnPropertyChanged(nameof(NothingHereMessage));
        ShowRows();
    }

    /// <inheritdoc cref="Notes.NotesViewModel.NothingHereMessage"/>
    public string NothingHereMessage => _everyShelf.Count > 0
        ? _translations["Nothing in this folder."]
        : _translations["No inventories yet."];

    /// <summary>
    /// Rebuilds the rows from what is already held. Separate from the read, so unlocking private things
    /// redraws without another round trip to the database.
    /// </summary>
    private void ShowRows()
    {
        Inventories.Clear();
        foreach (var inventory in _stored)
        {
            Inventories.Add(InventoryRow.From(
                inventory, _pending.Contains(inventory.LocalId), _networkStatus, _translations,
                _privateItems.IsUnlocked, _translations["Private"],
                // Every shelf, not the ones under this tab: a group may gather one filed somewhere
                // else, and a row that named only the members that happen to share its folder would be
                // telling half the truth. See InventoryRow.Gathering.
                _everyShelf));
        }

        // Also what marks the rows just drawn, through Changed.
        Picking.Shows(_stored.Select(inventory => new PickableThing(
            inventory.LocalId, inventory.ServerId, inventory.Name, inventory.IsShared, inventory.IsPrivate, inventory.IsArchived)));

        _unsearchableInventoryCount = _everyShelf.Count(inventory => !CanBeSearched(inventory));
        ShowMatchingItems();
    }

    /// <summary>Which inventories still have changes waiting to go out - see LocalNoteRepository.</summary>
    private IReadOnlySet<Guid> _pending = new HashSet<Guid>();

    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        _syncState.RecordStarted();
        try
        {
            // The folders first, and always - see NotesViewModel, which says why.
            await _folderSynchronizer.SynchroniseAsync(cancellationToken);

            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            RecordSync(result);

            if (result.Sent + result.Received + result.RemovedLocally > 0)
            {
                await ShowStoredInventoriesAsync(cancellationToken);
            }
        }
        catch (HttpRequestException)
        {
            _syncState.RecordFailed();
        }
        catch (OperationCanceledException)
        {
            // The screen went away mid-sync.
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    /// <summary>"Offline" is only said when the phone actually believes it has no connection.</summary>
    /// <summary>
    /// A sync that never reached the server is not the same as one the server refused, and SyncState
    /// tells them apart from the phone's own belief about connectivity rather than from the result.
    /// </summary>
    private void RecordSync(SyncResult result)
    {
        if (result.ReachedTheServer)
        {
            _syncState.RecordSucceeded();
            return;
        }

        _syncState.RecordFailed();
    }
    partial void OnNewInventoryNameChanged(string value) => AddInventoryCommand.NotifyCanExecuteChanged();
}

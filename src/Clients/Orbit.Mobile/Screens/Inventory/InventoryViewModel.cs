using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Security;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>Warehouses, read from the local database exactly as the other three features are.</summary>
public sealed partial class InventoryViewModel : ObservableObject
{
    private readonly LocalWarehouseRepository _warehouses;
    private readonly WarehouseSynchronizer _synchronizer;
    private readonly INetworkStatus _networkStatus;
    private readonly SyncState _syncState;
    private readonly IScreenNavigator _navigator;
    private readonly Translations _translations;
    private readonly PrivateItemGate _privateItems;

    [ObservableProperty]
    private string _newWarehouseName = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    /// <summary>
    /// The warehouses on screen, items and all, kept so the search can read them without going back to
    /// the database on every keystroke. Refreshed wherever the rows are.
    /// </summary>
    private IReadOnlyList<LocalWarehouse> _stored = [];

    /// <summary>
    /// Warehouses this device could not look inside - sealed with a key it has not got, or private
    /// while private things are locked. Counted rather than skipped: a search that quietly leaves one
    /// out answers "it is nowhere" when the truth is "I could not look there". Counted rather than
    /// named, because a name is one of the things being kept back.
    /// </summary>
    private int _unsearchableWarehouseCount;

    public InventoryViewModel(
        LocalWarehouseRepository warehouses, WarehouseSynchronizer synchronizer, INetworkStatus networkStatus,
        PrivateItemGate privateItems, SyncState syncState, IScreenNavigator navigator, Translations translations)
    {
        _warehouses = warehouses;
        _synchronizer = synchronizer;
        _networkStatus = networkStatus;
        _privateItems = privateItems;
        _syncState = syncState;
        _navigator = navigator;
        _translations = translations;
    }

    public ObservableCollection<WarehouseRow> Warehouses { get; } = [];


    /// <summary>
    /// What the reader is looking for across every warehouse. This page lists shelves and not what is on
    /// them, so where something is was the one question it could not answer.
    /// </summary>
    [ObservableProperty]
    private string _searchedItemName = string.Empty;

    /// <summary>What was found, and on which shelf - see <see cref="InventoryItemMatch"/>.</summary>
    public ObservableCollection<InventoryItemMatch> ItemMatches { get; } = [];

    /// <summary>The list of shelves steps aside while a search is on, since the answer replaces it.</summary>
    public bool IsSearchingItems => SearchedItemName.Trim().Length > 0;

    public bool IsShowingWarehouses => !IsSearchingItems;

    /// <summary>An empty shelf list and a search that found nothing need different words.</summary>
    public bool FoundNothing => IsSearchingItems && ItemMatches.Count == 0;

    /// <summary>
    /// What was found, and - when a warehouse is sealed with a key this phone has not got - that the
    /// answer is short of those. Saying only the count would let "nothing found" stand for "I could not
    /// look there", which is the one answer a search must never give by accident.
    /// </summary>
    public string ItemMatchSummary
        => _unsearchableWarehouseCount == 0
            ? _translations.Format("Found in {0} of {1} warehouses.", WarehousesMatched, _stored.Count)
            : _translations.Format(
                "Found in {0} of {1} warehouses. {2} could not be opened, so nothing in them was searched.",
                WarehousesMatched, _stored.Count, _unsearchableWarehouseCount);

    private int WarehousesMatched
        => ItemMatches.Select(match => match.WarehouseLocalId).Distinct().Count();

    [RelayCommand]
    private void ClearItemSearch() => SearchedItemName = string.Empty;

    [RelayCommand]
    private void OpenMatch(InventoryItemMatch? match)
    {
        if (match is not null)
        {
            _navigator.ShowWarehouse(match.WarehouseLocalId);
        }
    }

    /// <summary>
    /// Answers "which warehouse is this in", from what the phone already holds rather than by asking the
    /// server. Every warehouse's items came down with the warehouse, so there is nothing to fetch and
    /// nothing to cache - and a private warehouse keeps no item rows on the server at all, so an
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
            var found = _stored
                .Where(CanBeSearched)
                .SelectMany(warehouse => warehouse.Items.Select(item => new InventoryItemMatch(
                    warehouse.LocalId, warehouse.Name, WarehouseItemRow.From(item, _translations))))
                .Where(match => match.Name.Contains(wanted, StringComparison.CurrentCultureIgnoreCase))
                .OrderBy(match => match.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(match => match.WarehouseName, StringComparer.CurrentCultureIgnoreCase);

            foreach (var match in found)
            {
                ItemMatches.Add(match);
            }
        }

        OnPropertyChanged(nameof(IsSearchingItems));
        OnPropertyChanged(nameof(IsShowingWarehouses));
        OnPropertyChanged(nameof(FoundNothing));
        OnPropertyChanged(nameof(ItemMatchSummary));
    }

    partial void OnSearchedItemNameChanged(string value) => ShowMatchingItems();

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await ShowStoredWarehousesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanAddWarehouse))]
    private async Task AddWarehouseAsync(CancellationToken cancellationToken)
    {
        await _warehouses.CreateAsync(NewWarehouseName.Trim(), cancellationToken);
        NewWarehouseName = string.Empty;

        await ShowStoredWarehousesAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private bool CanAddWarehouse => NewWarehouseName.Trim().Length > 0;

    /// <inheritdoc cref="Notes.NotesViewModel.Open"/>
    [RelayCommand]
    private void OpenWarehouse(WarehouseRow? row)
    {
        if (row is { CanBeOpened: true })
        {
            _navigator.ShowWarehouse(row.LocalId);
        }
    }

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
    /// Whether a search may look inside. A sealed warehouse holds nothing this device could read, and a
    /// private one while private things are locked holds nothing it may show - see PrivateItemGate.
    /// </summary>
    private bool CanBeSearched(LocalWarehouse warehouse)
        => !warehouse.IsSealed && (!warehouse.IsPrivate || _privateItems.IsUnlocked);

    private async Task ShowStoredWarehousesAsync(CancellationToken cancellationToken)
    {
        _stored = await _warehouses.GetAllAsync(cancellationToken);
        var pending = await _warehouses.GetPendingLocalIdsAsync(cancellationToken);

        _pending = pending;
        ShowRows();
    }

    /// <summary>
    /// Rebuilds the rows from what is already held. Separate from the read, so unlocking private things
    /// redraws without another round trip to the database.
    /// </summary>
    private void ShowRows()
    {
        Warehouses.Clear();
        foreach (var warehouse in _stored)
        {
            Warehouses.Add(WarehouseRow.From(
                warehouse, _pending.Contains(warehouse.LocalId), _networkStatus, _translations,
                _privateItems.IsUnlocked, _translations["Private"]));
        }

        _unsearchableWarehouseCount = _stored.Count(warehouse => !CanBeSearched(warehouse));
        ShowMatchingItems();
    }

    /// <summary>Which warehouses still have changes waiting to go out - see LocalNoteRepository.</summary>
    private IReadOnlySet<Guid> _pending = new HashSet<Guid>();

    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        _syncState.RecordStarted();
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            RecordSync(result);

            if (result.Sent + result.Received + result.RemovedLocally > 0)
            {
                await ShowStoredWarehousesAsync(cancellationToken);
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
    partial void OnNewWarehouseNameChanged(string value) => AddWarehouseCommand.NotifyCanExecuteChanged();
}

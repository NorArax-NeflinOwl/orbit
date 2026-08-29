using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
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

    [ObservableProperty]
    private string _newWarehouseName = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    public InventoryViewModel(
        LocalWarehouseRepository warehouses, WarehouseSynchronizer synchronizer, INetworkStatus networkStatus,
        SyncState syncState, IScreenNavigator navigator, Translations translations)
    {
        _warehouses = warehouses;
        _synchronizer = synchronizer;
        _networkStatus = networkStatus;
        _syncState = syncState;
        _navigator = navigator;
        _translations = translations;
    }

    public ObservableCollection<WarehouseRow> Warehouses { get; } = [];

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

    [RelayCommand]
    private void OpenWarehouse(WarehouseRow? row)
    {
        if (row is not null)
        {
            _navigator.ShowWarehouse(row.LocalId);
        }
    }

    private async Task ShowStoredWarehousesAsync(CancellationToken cancellationToken)
    {
        var stored = await _warehouses.GetAllAsync(cancellationToken);
        var pending = await _warehouses.GetPendingLocalIdsAsync(cancellationToken);

        Warehouses.Clear();
        foreach (var warehouse in stored)
        {
            Warehouses.Add(WarehouseRow.From(warehouse, pending.Contains(warehouse.LocalId), _networkStatus, _translations));
        }
    }

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

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>Warehouses, read from the local database exactly as the other three features are.</summary>
public sealed partial class InventoryViewModel : ObservableObject
{
    private readonly LocalWarehouseRepository _warehouses;
    private readonly WarehouseSynchronizer _synchronizer;
    private readonly INetworkStatus _networkStatus;
    private readonly IScreenNavigator _navigator;

    [ObservableProperty]
    private string _syncStatus = string.Empty;

    [ObservableProperty]
    private string _newWarehouseName = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    public InventoryViewModel(
        LocalWarehouseRepository warehouses, WarehouseSynchronizer synchronizer, INetworkStatus networkStatus,
        IScreenNavigator navigator)
    {
        _warehouses = warehouses;
        _synchronizer = synchronizer;
        _networkStatus = networkStatus;
        _navigator = navigator;
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

    [RelayCommand]
    private void GoBack() => _navigator.ShowNotes();

    private async Task ShowStoredWarehousesAsync(CancellationToken cancellationToken)
    {
        var stored = await _warehouses.GetAllAsync(cancellationToken);
        var pending = await _warehouses.GetPendingLocalIdsAsync(cancellationToken);

        Warehouses.Clear();
        foreach (var warehouse in stored)
        {
            Warehouses.Add(WarehouseRow.From(warehouse, pending.Contains(warehouse.LocalId), _networkStatus));
        }
    }

    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        IsRefreshing = true;
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            SyncStatus = DescribeSync(result);

            if (result.Sent + result.Received + result.RemovedLocally > 0)
            {
                await ShowStoredWarehousesAsync(cancellationToken);
            }
        }
        catch (HttpRequestException)
        {
            SyncStatus = "Couldn't sync just now";
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
    private string DescribeSync(SyncResult result)
    {
        if (result.ReachedTheServer)
        {
            return result.Sent > 0 ? $"Synced - sent {result.Sent}" : "Synced";
        }

        return _networkStatus.IsOnline
            ? "Couldn't sync just now - your changes are saved on this phone"
            : "Offline - showing what's on this phone";
    }

    partial void OnNewWarehouseNameChanged(string value) => AddWarehouseCommand.NotifyCanExecuteChanged();
}

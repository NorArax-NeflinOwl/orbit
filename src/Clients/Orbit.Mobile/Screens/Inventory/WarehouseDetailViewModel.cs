using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Inventory;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// One warehouse and what it holds. Every change writes the <b>whole</b> item list, because that is what
/// the API's save means - anything missing from it is deleted - so the screen always sends the list it
/// is showing rather than a description of what changed.
/// </summary>
public sealed partial class WarehouseDetailViewModel : ObservableObject
{
    private readonly LocalWarehouseRepository _warehouses;
    private readonly WarehouseSynchronizer _synchronizer;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    private Guid _localId;
    private IReadOnlyList<WarehouseItemDto> _items = [];

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _newItemName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    public WarehouseDetailViewModel(
        LocalWarehouseRepository warehouses, WarehouseSynchronizer synchronizer, Translations translations,
        IScreenNavigator navigator)
    {
        _warehouses = warehouses;
        _synchronizer = synchronizer;
        _translations = translations;
        _navigator = navigator;
    }

    public ObservableCollection<WarehouseItemDto> Items { get; } = [];

    public bool HasStatus => Status.Length > 0;

    public bool CanEdit => !IsReadOnly;

    public void Open(Guid localId) => _localId = localId;

    [RelayCommand]
    private Task LoadAsync(CancellationToken cancellationToken) => ShowStoredWarehouseAsync(cancellationToken);

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private Task AddItemAsync(CancellationToken cancellationToken)
    {
        var name = NewItemName.Trim();
        NewItemName = string.Empty;

        // No id: this one has never been saved, and claiming an id nothing has would be a lie the server
        // would have to sort out. See WarehouseItemDto.Id.
        return SaveAsync(
            [.. _items, new WarehouseItemDto(null, name, "Piece", "General", 1, null, null, "None")],
            cancellationToken);
    }

    private bool CanAddItem => NewItemName.Trim().Length > 0;

    [RelayCommand]
    private Task AddOneAsync(WarehouseItemDto? item, CancellationToken cancellationToken)
        => ChangeQuantityAsync(item, by: 1, cancellationToken);

    [RelayCommand]
    private Task RemoveOneAsync(WarehouseItemDto? item, CancellationToken cancellationToken)
        => ChangeQuantityAsync(item, by: -1, cancellationToken);

    /// <summary>Never below zero - a negative count of something on a shelf is not a state that exists.</summary>
    private Task ChangeQuantityAsync(WarehouseItemDto? item, decimal by, CancellationToken cancellationToken)
        => item is null
            ? Task.CompletedTask
            : SaveAsync(
                _items.Select(candidate => candidate.Id == item.Id
                        ? candidate with { Quantity = Math.Max(0, candidate.Quantity + by) }
                        : candidate)
                    .ToList(),
                cancellationToken);

    [RelayCommand]
    private Task RemoveItemAsync(WarehouseItemDto? item, CancellationToken cancellationToken)
        => item is null
            ? Task.CompletedTask
            : SaveAsync(_items.Where(candidate => candidate.Id != item.Id).ToList(), cancellationToken);

    [RelayCommand]
    private void GoBack() => _navigator.ShowInventory();

    private async Task SaveAsync(IReadOnlyList<WarehouseItemDto> items, CancellationToken cancellationToken)
    {
        var outcome = await _warehouses.UpdateAsync(_localId, Name, items, cancellationToken);
        if (outcome is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations[
                "Somebody else can change this warehouse, and Orbit can't be reached to check. "
                + "It stays read-only until you're back online."];
            return;
        }

        await ShowStoredWarehouseAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private async Task ShowStoredWarehouseAsync(CancellationToken cancellationToken)
    {
        if (await _warehouses.FindAsync(_localId, cancellationToken) is not { } warehouse)
        {
            _navigator.ShowInventory();
            return;
        }

        Name = warehouse.Name;
        _items = warehouse.Items;
        IsReadOnly = !await _warehouses.CanEditAsync(_localId, cancellationToken);

        Items.Clear();
        foreach (var item in warehouse.Items)
        {
            Items.Add(item);
        }
    }

    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            Status = result.ReachedTheServer ? string.Empty : _translations["Saved on this phone - it will sync later"];

            if (result.Received > 0)
            {
                await ShowStoredWarehouseAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Status = _translations["Saved on this phone - it will sync later"];
        }
    }

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsReadOnlyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));

    partial void OnNewItemNameChanged(string value) => AddItemCommand.NotifyCanExecuteChanged();
}

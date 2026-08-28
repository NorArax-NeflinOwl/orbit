using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Inventory;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Mobile.Screens;
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
    private readonly InventoryClient _inventoryClient;
    private readonly EditLock _editLock;
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
        SharePanel share, IScreenNavigator navigator,
        InventoryClient inventoryClient, EditLock editLock)
    {
        _warehouses = warehouses;
        _synchronizer = synchronizer;
        _translations = translations;
        Share = share;
        _navigator = navigator;
        _inventoryClient = inventoryClient;
        _editLock = editLock;
        _editLock.Changed += (_, _) => ShowWhoElseIsEditing();
    }

    public ObservableCollection<WarehouseItemRow> Items { get; } = [];

    /// <summary>
    /// The item whose details are open, or null while the list is. One at a time and in place rather
    /// than on a screen of its own: a warehouse is a list of small things, and a page per row would be
    /// two taps away from everything.
    /// </summary>
    [ObservableProperty]
    private WarehouseItemEditor? _beingEdited;

    public bool IsEditingItem => BeingEdited is not null;

    public bool IsShowingList => BeingEdited is null;

    /// <summary>Offering this to somebody else - see SharePanel.</summary>
    public SharePanel Share { get; }

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
    private Task AddOneAsync(WarehouseItemRow? row, CancellationToken cancellationToken)
        => ChangeQuantityAsync(row?.Item, by: 1, cancellationToken);

    [RelayCommand]
    private Task RemoveOneAsync(WarehouseItemRow? row, CancellationToken cancellationToken)
        => ChangeQuantityAsync(row?.Item, by: -1, cancellationToken);

    /// <summary>Opens one item's details - what kind of thing it is, its minimum, when it goes off.</summary>
    [RelayCommand]
    private void EditItem(WarehouseItemRow? row)
    {
        if (row is not null && CanEdit)
        {
            BeingEdited = WarehouseItemEditor.For(row.Item, _translations);
        }
    }

    [RelayCommand]
    private void CancelItemEdit() => BeingEdited = null;

    [RelayCommand]
    private Task SaveItemAsync(CancellationToken cancellationToken)
    {
        if (BeingEdited is not { CanSave: true } editor)
        {
            return Task.CompletedTask;
        }

        var edited = editor.ToDto();
        BeingEdited = null;

        // Matched by id, and by name for one that has never been saved - a new item has no id until the
        // push comes back with one. See WarehouseItemDto.Id.
        return SaveAsync(
            [.. _items.Select(candidate => Matches(candidate, edited) ? edited : candidate)],
            cancellationToken);
    }

    private static bool Matches(WarehouseItemDto candidate, WarehouseItemDto edited)
        => edited.Id is { } id ? candidate.Id == id : candidate.Id is null && candidate.Name == edited.Name;

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
    private Task RemoveItemAsync(WarehouseItemRow? row, CancellationToken cancellationToken)
        => row is null
            ? Task.CompletedTask
            : SaveAsync([.. _items.Where(candidate => !Matches(candidate, row.Item))], cancellationToken);

    /// <inheritdoc cref="Tasks.TaskListDetailViewModel.RenameAsync"/>
    [RelayCommand]
    private Task RenameAsync(CancellationToken cancellationToken) => SaveAsync(_items, cancellationToken);

    /// <summary>
    /// Gets rid of the whole warehouse, which the phone could not do from anywhere - Orbit.Web has had
    /// it all along, and the local store and the client both already knew how.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var outcome = await _warehouses.DeleteAsync(_localId, cancellationToken);
        if (outcome is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations[
                "Somebody else can change this warehouse, and Orbit can't be reached to check. "
                + "It stays read-only until you're back online."];
            return;
        }

        await SynchroniseAsync(cancellationToken);
        _navigator.ShowInventory();
    }

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
        if (warehouse.ServerId is { } serverId)
        {
            Share.Describes(
                SharedItemKind.Warehouse, serverId, warehouse.Name,
                warehouse.AccessLevel == "CanEdit" ? null : warehouse.OwnerUserId);
        }

        _items = warehouse.Items;
        IsReadOnly = !await _warehouses.CanEditAsync(_localId, cancellationToken);
        ReadOnlyReason = string.Empty;

        if (!IsReadOnly && warehouse.ServerId is { } lockedServerId)
        {
            // Claimed for as long as this screen is open, so somebody editing the same thing on the web
            // is told rather than left to have their save refused - see EditLock.
            await _editLock.HoldAsync(_inventoryClient, lockedServerId, cancellationToken);
            ShowWhoElseIsEditing();
        }

        Items.Clear();
        foreach (var item in warehouse.Items)
        {
            Items.Add(WarehouseItemRow.From(item, _translations));
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

    partial void OnBeingEditedChanged(WarehouseItemEditor? value)
    {
        OnPropertyChanged(nameof(IsEditingItem));
        OnPropertyChanged(nameof(IsShowingList));
    }

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsReadOnlyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));

    partial void OnNewItemNameChanged(string value) => AddItemCommand.NotifyCanExecuteChanged();

    /// <summary>Why it cannot be changed right now - empty when it can, which is the common case.</summary>
    [ObservableProperty]
    private string _readOnlyReason = string.Empty;

    public bool HasReadOnlyReason => ReadOnlyReason.Length > 0;

    private void ShowWhoElseIsEditing()
    {
        if (!_editLock.IsHeldByAnother)
        {
            return;
        }

        IsReadOnly = true;
        ReadOnlyReason = _editLock.RefusalMessage;
    }

    /// <summary>Lets it go when the screen does, rather than leaving it claimed for a minute.</summary>
    public Task CloseAsync() => _editLock.ReleaseAsync();

    partial void OnReadOnlyReasonChanged(string value) => OnPropertyChanged(nameof(HasReadOnlyReason));
}

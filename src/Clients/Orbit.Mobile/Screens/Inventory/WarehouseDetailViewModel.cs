using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Inventory;
using Orbit.Core.Inventory;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Core.Suggestions;
using Orbit.Mobile.Screens.Suggestions;
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
    private readonly PrivateContentSealer _privateContent;
    private readonly NameSuggestions _nameSuggestions;
    private readonly NameSuggestions _warehouseNameSuggestions;
    private readonly IScreenNavigator _navigator;

    private Guid _localId;
    private IReadOnlyList<WarehouseItemDto> _items = [];

    /// <summary>What is on screen has been narrowed down to - see <see cref="WarehouseItemFilter"/>.</summary>
    private readonly WarehouseItemFilter _filter = new();

    /// <summary>
    /// What the two pickers say when nothing is chosen. Held rather than looked up each time because the
    /// chosen value is compared against it, and a dictionary lookup per comparison would be the only
    /// thing standing between a filter and the whole shelf.
    /// </summary>
    private readonly string _anyProductType;
    private readonly string _anyCategory;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _newItemName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    /// <summary>
    /// Only its owner may ever read this warehouse, and the server never can. Orbit.Web's warehouse
    /// editor has had the checkbox all along; the phone carried the flag without being able to set one -
    /// see PrivateContentSealer.
    /// </summary>
    [ObservableProperty]
    private bool _isPrivate;

    public WarehouseDetailViewModel(
        LocalWarehouseRepository warehouses, WarehouseSynchronizer synchronizer, Translations translations,
        SharePanel share, IScreenNavigator navigator,
        InventoryClient inventoryClient, EditLock editLock, PrivateContentSealer privateContent,
        NameSuggestions nameSuggestions, NameSuggestions warehouseNameSuggestions)
    {
        _warehouses = warehouses;
        _synchronizer = synchronizer;
        _translations = translations;
        Share = share;
        _navigator = navigator;
        _inventoryClient = inventoryClient;
        _privateContent = privateContent;
        _nameSuggestions = nameSuggestions;
        OfferNamesToTheQuickAddBox();
        _warehouseNameSuggestions = warehouseNameSuggestions;
        _warehouseNameSuggestions.Offers(NameSuggestionKind.WarehouseName);
        _warehouseNameSuggestions.Takes = name => Name = name;
        _editLock = editLock;
        _editLock.Changed += (_, _) => ShowWhoElseIsEditing();
        _anyProductType = translations["Any type"];
        _anyCategory = translations["Any category"];
        ChosenProductType = _anyProductType;
        ChosenCategory = _anyCategory;
    }

    public ObservableCollection<WarehouseItemRow> Items { get; } = [];

    /// <summary>True while the screen fills itself in, so loading does not look like a person choosing.</summary>
    private bool _isShowingWhatIsStored;

    /// <summary>Saved as soon as it is switched, the way ticking an entry on a list is.</summary>
    partial void OnIsPrivateChanged(bool value)
    {
        if (!_isShowingWhatIsStored && !IsReadOnly)
        {
            RenameCommand.Execute(null);
        }
    }

    /// <summary>
    /// The types and categories actually on this shelf, each behind an "any" that stands for no choice -
    /// a filter offering something nothing is filed under is a dead end.
    /// </summary>
    public ObservableCollection<string> ProductTypes { get; } = [];

    public ObservableCollection<string> Categories { get; } = [];

    /// <summary>
    /// Nullable because the platform makes it so: emptying a bound Picker's items sets its selection to
    /// nothing, and the binding writes that null back here - which happens on every reload, before the
    /// options are put back. See <see cref="ShowWhatIsOnTheShelf"/>.
    /// </summary>
    [ObservableProperty]
    private string? _chosenProductType;

    /// <inheritdoc cref="ChosenProductType"/>
    [ObservableProperty]
    private string? _chosenCategory;

    /// <summary>
    /// What the reader has typed to find something by name. Narrows as it is typed rather than on Done:
    /// the rows are already on screen, so there is nothing to wait for.
    /// </summary>
    [ObservableProperty]
    private string _searchedName = string.Empty;

    /// <summary>
    /// The two pickers are offered one by one, each only where something on the shelf is filed under it -
    /// a filter whose one answer is "any" is a dead end. Searching by name has no such condition and is
    /// always offered: a name is typed, and every item has one.
    /// </summary>
    public bool CanNarrowByProductType => ProductTypes.Count > 1;

    /// <inheritdoc cref="CanNarrowByProductType"/>
    public bool CanNarrowByCategory => Categories.Count > 1;

    /// <summary>Hidden along with the rows themselves while the shelf is empty - there is nothing to search.</summary>
    public bool CanNarrow => _items.Count > 0;

    public bool IsNarrowed => _filter.IsActive;

    /// <summary>
    /// Said out loud while the shelf is narrowed, so nobody saves a warehouse thinking the rows they
    /// cannot see are gone.
    /// </summary>
    public string FilterNote => _filter.IsActive
        ? _translations.Format("Showing {0} of {1} items. Saving keeps all of them.", Items.Count, _items.Count)
        : string.Empty;

    /// <summary>An empty shelf and a shelf whose rows are all hidden are different situations.</summary>
    public string EmptyMessage => _filter.IsActive
        ? _translations["Nothing here matches that. The rest of the warehouse is still there - clear the filter to see it."]
        : _translations["Nothing in this warehouse yet."];

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

        // A row added while the shelf is narrowed is filed under nothing yet, so it would be hidden the
        // moment it appeared. The filter steps aside for it, as it does on the web.
        ShowEverything();

        // No id: this one has never been saved, and claiming an id nothing has would be a lie the server
        // would have to sort out. See WarehouseItemDto.Id.
        //
        // No type and no category either, as Orbit.Web adds one: those are the reader's words for what
        // the thing is, and a phone has no business inventing them. It used to write "Piece" and
        // "General" - English on a Polish shelf, a unit's name in the field for a kind of thing, and two
        // made-up values in the filters above.
        return SaveAsync(
            [.. _items, new WarehouseItemDto(
                null, name, string.Empty, string.Empty, 1, null, nameof(InventoryUnit.Piece), null, "None")],
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
            BeingEdited = WarehouseItemEditor.For(row.Item, _translations, _nameSuggestions);
        }
    }

    [RelayCommand]
    private void CancelItemEdit() => BeingEdited = null;

    /// <summary>Puts the whole shelf back, whatever the two pickers were narrowed to.</summary>
    [RelayCommand]
    private void ShowEverything()
    {
        ChosenProductType = _anyProductType;
        ChosenCategory = _anyCategory;
        SearchedName = string.Empty;
    }

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

    /// <summary>
    /// Moves a product one place up the shelf, or one place down. The order a warehouse is saved in is
    /// the order it is stored in - see InventoryItem.Position - so arranging it here is how it reads
    /// everywhere, which is the same thing Orbit.Web's drag handles do.
    ///
    /// One place among what is <i>shown</i>, not among what is stored. A narrowed shelf hides rows, and
    /// swapping with a hidden neighbour would move the product without anything on screen changing -
    /// which reads as a button that does nothing.
    /// </summary>
    [RelayCommand]
    private Task MoveItemUpAsync(WarehouseItemRow? row, CancellationToken cancellationToken)
        => MoveItemAsync(row, by: -1, cancellationToken);

    [RelayCommand]
    private Task MoveItemDownAsync(WarehouseItemRow? row, CancellationToken cancellationToken)
        => MoveItemAsync(row, by: 1, cancellationToken);

    private Task MoveItemAsync(WarehouseItemRow? row, int by, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return Task.CompletedTask;
        }

        var shown = _items.Where(_filter.Matches).ToList();
        var shownFrom = shown.FindIndex(candidate => Matches(candidate, row.Item));
        var shownTo = shownFrom + by;

        // The ends are where a shelf stops, not a failure: the top row has nowhere above it.
        if (shownFrom < 0 || shownTo < 0 || shownTo >= shown.Count)
        {
            return Task.CompletedTask;
        }

        // Taken out and put back on the far side of the row it passed, rather than swapped: with rows
        // hidden between the two, swapping would carry the hidden ones along with it.
        var reordered = _items.ToList();
        var moved = reordered.Single(candidate => Matches(candidate, row.Item));
        reordered.Remove(moved);

        var passed = reordered.FindIndex(candidate => Matches(candidate, shown[shownTo]));
        reordered.Insert(by > 0 ? passed + 1 : passed, moved);

        return SaveAsync(reordered, cancellationToken);
    }

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
        LocalWriteOutcome outcome;
        try
        {
            outcome = await _warehouses.UpdateAsync(
                _localId, new WarehouseContent(Name, items, IsPrivate), cancellationToken);
        }
        catch (EncryptionKeyLockedException)
        {
            // Sealing needs the account's own key, and this device has not got it - see
            // NoteDetailViewModel, which sends the reader to the same gate for the same reason.
            _navigator.ShowChatKeyGate();
            return;
        }

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
        // Taken as already looked up, so opening a warehouse does not offer completions of its own name
        // and warn that it duplicates itself - see NameSuggestions.StartsAt.
        _warehouseNameSuggestions.StartsAt(warehouse.Name);
        _isShowingWhatIsStored = true;
        IsPrivate = warehouse.IsPrivate;
        _isShowingWhatIsStored = false;

        // A private warehouse is offered to nobody: the server holds no readable copy to hand over,
        // which is what makes it private - the same line Orbit.Web's editor draws.
        if (warehouse is { ServerId: { } serverId, IsPrivate: false })
        {
            Share.Describes(
                SharedItemKind.Warehouse, serverId, warehouse.Name,
                warehouse.AccessLevel == "CanEdit" ? null : warehouse.OwnerUserId);
        }
        else
        {
            Share.OffersNothing();
        }

        _items = warehouse.Items;

        // Sealed with a key this device cannot open - see TaskListDetailViewModel for the same guard
        // and why saving one anyway is worse than not offering to.
        if (warehouse.IsSealed)
        {
            IsReadOnly = true;
            ReadOnlyReason = await _privateContent.HasKeyAsync(cancellationToken)
                ? _translations["This warehouse was sealed with an encryption key this account no longer has."]
                : _translations["This warehouse is private. Unlock this device's encryption key to read it."];
        }
        else
        {
            IsReadOnly = !await _warehouses.CanEditAsync(_localId, cancellationToken);
            ReadOnlyReason = string.Empty;
        }

        if (!IsReadOnly && warehouse.ServerId is { } lockedServerId)
        {
            // Claimed for as long as this screen is open, so somebody editing the same thing on the web
            // is told rather than left to have their save refused - see EditLock.
            await _editLock.HoldAsync(_inventoryClient, lockedServerId, cancellationToken);
            ShowWhoElseIsEditing();
        }

        ShowWhatIsOnTheShelf();
    }

    /// <summary>
    /// Rebuilds the rows and what the pickers offer from <see cref="_items"/>. A choice whose last item
    /// has gone stands for nothing, so it steps aside rather than hiding the whole shelf.
    /// </summary>
    private void ShowWhatIsOnTheShelf()
    {
        Offer(ProductTypes, _anyProductType, item => item.ProductType);
        Offer(Categories, _anyCategory, item => item.Category);

        if (ChosenProductType is not { } productType || !ProductTypes.Contains(productType))
        {
            ChosenProductType = _anyProductType;
        }

        if (ChosenCategory is not { } category || !Categories.Contains(category))
        {
            ChosenCategory = _anyCategory;
        }

        ShowMatchingRows();
        OnPropertyChanged(nameof(CanNarrow));
        OnPropertyChanged(nameof(CanNarrowByProductType));
        OnPropertyChanged(nameof(CanNarrowByCategory));
    }

    private void Offer(ObservableCollection<string> options, string forAny, Func<WarehouseItemDto, string> of)
    {
        var onTheShelf = _items
            .Select(item => of(item).Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(value => value, StringComparer.CurrentCultureIgnoreCase);

        options.Clear();
        options.Add(forAny);
        foreach (var value in onTheShelf)
        {
            options.Add(value);
        }
    }

    private void ShowMatchingRows()
    {
        Items.Clear();
        foreach (var item in _items.Where(_filter.Matches))
        {
            Items.Add(WarehouseItemRow.From(item, _translations));
        }

        OnPropertyChanged(nameof(IsNarrowed));
        OnPropertyChanged(nameof(FilterNote));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    partial void OnChosenProductTypeChanged(string? value)
    {
        _filter.ProductType = Narrowing(value, _anyProductType);
        ShowMatchingRows();
    }

    partial void OnChosenCategoryChanged(string? value)
    {
        _filter.Category = Narrowing(value, _anyCategory);
        ShowMatchingRows();
    }

    partial void OnSearchedNameChanged(string value)
    {
        _filter.Name = value;
        ShowMatchingRows();
    }

    /// <summary>
    /// What a chosen option narrows the shelf by - nothing, for the "any" entry and for the null a
    /// cleared Picker writes back. Both mean "not narrowed"; only one of them is a person choosing.
    /// </summary>
    private static string Narrowing(string? chosen, string forAny)
        => chosen is null || chosen == forAny ? string.Empty : chosen;

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
        // Nothing on offer once the form is gone, and the box above the list takes what is chosen
        // again - it is the field being typed into whenever no editor is.
        if (value is null)
        {
            OfferNamesToTheQuickAddBox();
        }

        OnPropertyChanged(nameof(IsEditingItem));
        OnPropertyChanged(nameof(IsShowingList));
    }

    /// <summary>
    /// Products already on the shelves, offered under whichever field is being typed into - the box
    /// above the list, or an item's name once one is open. One at a time, because only one of the two
    /// is ever on screen.
    /// </summary>
    public NameSuggestions Suggestions => _nameSuggestions;

    /// <summary>
    /// Warehouse names this account already has, offered under the name field. Its own instance rather
    /// than the one above: the name and the quick-add box are on screen together, and one instance
    /// serves one field - see NameSuggestions.Takes.
    /// </summary>
    public NameSuggestions WarehouseNameSuggestions => _warehouseNameSuggestions;

    private void OfferNamesToTheQuickAddBox()
    {
        _nameSuggestions.Forget();
        _nameSuggestions.Offers(NameSuggestionKind.InventoryItemName);
        _nameSuggestions.Takes = name => NewItemName = name;
    }

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsReadOnlyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));

    partial void OnNewItemNameChanged(string value)
    {
        AddItemCommand.NotifyCanExecuteChanged();
        Suggestions.ShowFor(value);
    }

    partial void OnNameChanged(string value) => WarehouseNameSuggestions.ShowFor(value);

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

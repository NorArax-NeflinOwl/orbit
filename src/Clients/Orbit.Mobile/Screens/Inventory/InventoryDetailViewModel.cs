using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Inventories;
using Orbit.Core.Inventories;
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
/// One inventory and what it holds. Every change writes the <b>whole</b> item list, because that is what
/// the API's save means - anything missing from it is deleted - so the screen always sends the list it
/// is showing rather than a description of what changed.
/// </summary>
public sealed partial class InventoryDetailViewModel : ObservableObject
{
    private readonly LocalInventoryRepository _inventories;

    /// <summary>Only to say why this is read-only, in the same words the inventory's own rows use.</summary>
    private readonly INetworkStatus _networkStatus;
    private readonly InventorySynchronizer _synchronizer;
    private readonly InventoryClient _inventoryClient;
    private readonly EditLock _editLock;
    private readonly Translations _translations;
    private readonly PrivateContentSealer _privateContent;
    private readonly NameSuggestions _nameSuggestions;
    private readonly NameSuggestions _inventoryNameSuggestions;
    private readonly IScreenNavigator _navigator;

    private Guid _localId;
    private IReadOnlyList<InventoryItemRequest> _items = [];

    /// <summary>When each batch arrived, by its id - see LocalInventory.ItemArrivals.</summary>
    private IReadOnlyDictionary<Guid, DateTimeOffset> _arrivals = new Dictionary<Guid, DateTimeOffset>();

    /// <summary>What is on screen has been narrowed down to - see <see cref="InventoryItemFilter"/>.</summary>
    private readonly InventoryItemFilter _filter = new();

    /// <summary>
    /// What the two pickers say when nothing is chosen. Held rather than looked up each time because the
    /// chosen value is compared against it, and a dictionary lookup per comparison would be the only
    /// thing standing between a filter and the whole shelf.
    /// </summary>
    private readonly string _anyProductType;
    private readonly string _anyCategory;

    [ObservableProperty]
    private string _name = string.Empty;

    /// <inheritdoc cref="Tasks.TaskListDetailViewModel.Description"/>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <inheritdoc cref="Tasks.TaskListDetailViewModel._savedDescription"/>
    private string _savedDescription = string.Empty;

    /// <summary>
    /// Whether a description is worth offering at all: a private inventory keeps none, because a
    /// description stored in the clear would say in the open what the name is sealed to hide.
    /// </summary>
    public bool IsNotPrivate => !IsPrivate;

    [ObservableProperty]
    private string _newItemName = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    /// <summary>
    /// Only its owner may ever read this inventory, and the server never can. Orbit.Web's inventory
    /// editor has had the checkbox all along; the phone carried the flag without being able to set one -
    /// see PrivateContentSealer.
    /// </summary>
    [ObservableProperty]
    private bool _isPrivate;

    public InventoryDetailViewModel(
        LocalInventoryRepository inventories, InventorySynchronizer synchronizer, Translations translations,
        SharePanel share, IScreenNavigator navigator,
        InventoryClient inventoryClient, EditLock editLock, PrivateContentSealer privateContent,
        NameSuggestions nameSuggestions, NameSuggestions inventoryNameSuggestions,
        INetworkStatus networkStatus, RestockListSettingsPanel restockList)
    {
        _networkStatus = networkStatus;
        RestockList = restockList;
        _inventories = inventories;
        _synchronizer = synchronizer;
        _translations = translations;
        Share = share;
        _navigator = navigator;
        _inventoryClient = inventoryClient;
        _privateContent = privateContent;
        _nameSuggestions = nameSuggestions;
        OfferNamesToTheQuickAddBox();
        _inventoryNameSuggestions = inventoryNameSuggestions;
        _inventoryNameSuggestions.Offers(NameSuggestionKind.InventoryName);
        _inventoryNameSuggestions.Takes = name => Name = name;
        _editLock = editLock;
        _editLock.Changed += (_, _) => ShowWhoElseIsEditing();
        _anyProductType = translations["Any type"];
        _anyCategory = translations["Any category"];
        ChosenProductType = _anyProductType;
        ChosenCategory = _anyCategory;
    }

    public ObservableCollection<InventoryItemRow> Items { get; } = [];

    /// <summary>True while the screen fills itself in, so loading does not look like a person choosing.</summary>
    private bool _isShowingWhatIsStored;

    /// <summary>Saved as soon as it is switched, the way ticking an entry on a list is.</summary>
    /// <inheritdoc cref="Tasks.TaskListDetailViewModel.CommitDescriptionAsync"/>
    [RelayCommand]
    private Task CommitDescriptionAsync(CancellationToken cancellationToken)
    {
        if (Description == _savedDescription)
        {
            return Task.CompletedTask;
        }

        _savedDescription = Description;
        return RenameCommand.ExecuteAsync(null);
    }

    partial void OnIsPrivateChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotPrivate));

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
    /// Said out loud while the shelf is narrowed, so nobody saves an inventory thinking the rows they
    /// cannot see are gone.
    /// </summary>
    public string FilterNote => _filter.IsActive
        ? _translations.Format("Showing {0} of {1} items. Saving keeps all of them.", Items.Count, _items.Count)
        : string.Empty;

    /// <summary>An empty shelf and a shelf whose rows are all hidden are different situations.</summary>
    public string EmptyMessage => _filter.IsActive
        ? _translations["Nothing here matches that. The rest of the inventory is still there - clear the filter to see it."]
        : _translations["Nothing in this inventory yet."];

    /// <summary>
    /// The item whose details are open, or null while the list is. One at a time and in place rather
    /// than on a screen of its own: an inventory is a list of small things, and a page per row would be
    /// two taps away from everything.
    /// </summary>
    [ObservableProperty]
    private InventoryItemEditor? _beingEdited;

    public bool IsEditingItem => BeingEdited is not null;

    public bool IsShowingList => BeingEdited is null;

    /// <summary>Offering this to somebody else - see SharePanel.</summary>
    public SharePanel Share { get; }

    public bool HasStatus => Status.Length > 0;

    public bool CanEdit => !IsReadOnly;

    public void Open(Guid localId, Guid? productId = null)
    {
        _localId = localId;
        _pointedAtProductId = productId;
    }

    /// <summary>
    /// The product this shelf was opened for, when something meant one - see
    /// IScreenNavigator.ShowInventory. Kept for as long as the screen is, the way the browser keeps the
    /// ?highlight= it was opened with: narrowing the shelf and clearing the filter again should find the
    /// row still marked.
    /// </summary>
    private Guid? _pointedAtProductId;

    /// <summary>
    /// The row that was pointed at, for a list that has to bring it into view - a mark below the fold is
    /// a mark nobody sees. Null when nothing was pointed at, or when the filter is hiding it.
    /// </summary>
    public InventoryItemRow? PointedAtRow => Items.FirstOrDefault(row => row.IsPointedAt);

    [RelayCommand]
    private Task LoadAsync(CancellationToken cancellationToken) => ShowStoredInventoryAsync(cancellationToken);

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private Task AddItemAsync(CancellationToken cancellationToken)
    {
        var name = NewItemName.Trim();
        NewItemName = string.Empty;

        // A row added while the shelf is narrowed is filed under nothing yet, so it would be hidden the
        // moment it appeared. The filter steps aside for it, as it does on the web.
        ShowEverything();

        // No id: this one has never been saved, and claiming an id nothing has would be a lie the server
        // would have to sort out. See InventoryItemRequest.Id.
        //
        // No type and no category either, as Orbit.Web adds one: those are the reader's words for what
        // the thing is, and a phone has no business inventing them. It used to write "Piece" and
        // "General" - English on a Polish shelf, a unit's name in the field for a kind of thing, and two
        // made-up values in the filters above.
        return SaveAsync(
            [.. _items, new InventoryItemRequest(
                null, name, string.Empty, string.Empty, 1, null, nameof(InventoryUnit.Piece), null, "None")],
            cancellationToken);
    }

    private bool CanAddItem => NewItemName.Trim().Length > 0;

    [RelayCommand]
    private Task AddOneAsync(InventoryItemRow? row, CancellationToken cancellationToken)
        => ChangeQuantityAsync(row?.Item, by: 1, cancellationToken);

    [RelayCommand]
    private Task RemoveOneAsync(InventoryItemRow? row, CancellationToken cancellationToken)
        => ChangeQuantityAsync(row?.Item, by: -1, cancellationToken);

    /// <summary>Opens one item's details - what kind of thing it is, its minimum, when it goes off.</summary>
    [RelayCommand]
    private void EditItem(InventoryItemRow? row)
    {
        if (row is not null && CanEdit)
        {
            BeingEdited = InventoryItemEditor.For(row.Item, _translations, _nameSuggestions);
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
        // push comes back with one. See InventoryItemRequest.Id.
        return SaveAsync(
            [.. _items.Select(candidate => Matches(candidate, edited) ? edited : candidate)],
            cancellationToken);
    }

    private static bool Matches(InventoryItemRequest candidate, InventoryItemRequest edited)
        => edited.Id is { } id ? candidate.Id == id : candidate.Id is null && candidate.Name == edited.Name;

    /// <summary>Never below zero - a negative count of something on a shelf is not a state that exists.</summary>
    private Task ChangeQuantityAsync(InventoryItemRequest? item, decimal by, CancellationToken cancellationToken)
        => item is null
            ? Task.CompletedTask
            : SaveAsync(
                _items.Select(candidate => candidate.Id == item.Id
                        ? candidate with { Quantity = Math.Max(0, candidate.Quantity + by) }
                        : candidate)
                    .ToList(),
                cancellationToken);

    [RelayCommand]
    private Task RemoveItemAsync(InventoryItemRow? row, CancellationToken cancellationToken)
        => row is null
            ? Task.CompletedTask
            : SaveAsync([.. _items.Where(candidate => !Matches(candidate, row.Item))], cancellationToken);

    /// <summary>
    /// Moves a product one place up the shelf, or one place down. The order an inventory is saved in is
    /// the order it is stored in - see InventoryItem.Position - so arranging it here is how it reads
    /// everywhere, which is the same thing Orbit.Web's drag handles do.
    ///
    /// One place among what is <i>shown</i>, not among what is stored. A narrowed shelf hides rows, and
    /// swapping with a hidden neighbour would move the product without anything on screen changing -
    /// which reads as a button that does nothing.
    /// </summary>
    [RelayCommand]
    private Task MoveItemUpAsync(InventoryItemRow? row, CancellationToken cancellationToken)
        => MoveItemAsync(row, by: -1, cancellationToken);

    [RelayCommand]
    private Task MoveItemDownAsync(InventoryItemRow? row, CancellationToken cancellationToken)
        => MoveItemAsync(row, by: 1, cancellationToken);

    private Task MoveItemAsync(InventoryItemRow? row, int by, CancellationToken cancellationToken)
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
    /// Gets rid of the whole inventory, which the phone could not do from anywhere - Orbit.Web has had
    /// it all along, and the local store and the client both already knew how.
    /// </summary>
    [RelayCommand]
    private async Task DeleteAsync(CancellationToken cancellationToken)
    {
        var outcome = await _inventories.DeleteAsync(_localId, cancellationToken);
        if (outcome.WasRefused())
        {
            Status = outcome.Explain(RefusalMessage, _translations);
            return;
        }

        await SynchroniseAsync(cancellationToken);
        _navigator.ShowInventory();
    }

    [RelayCommand]
    private void GoBack() => _navigator.ShowInventory();

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string RefusalMessage =
        "Somebody else can change this warehouse, and Orbit can't be reached to check. "
        + "It stays read-only until you're back online.";

    private async Task SaveAsync(IReadOnlyList<InventoryItemRequest> items, CancellationToken cancellationToken)
    {
        LocalWriteOutcome outcome;
        try
        {
            outcome = await _inventories.UpdateAsync(
                _localId, new InventoryContent(Name, items, IsPrivate, Description), cancellationToken);
        }
        catch (EncryptionKeyLockedException)
        {
            // Sealing needs the account's own key, and this device has not got it - see
            // NoteDetailViewModel, which sends the reader to the same gate for the same reason.
            _navigator.ShowChatKeyGate();
            return;
        }

        if (outcome.WasRefused())
        {
            Status = outcome.Explain(RefusalMessage, _translations);
            return;
        }

        await ShowStoredInventoryAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private async Task ShowStoredInventoryAsync(CancellationToken cancellationToken)
    {
        if (await _inventories.FindAsync(_localId, cancellationToken) is not { } inventory)
        {
            _navigator.ShowInventory();
            return;
        }

        Name = inventory.Name;
        Description = inventory.Description;
        _savedDescription = inventory.Description;
        // Taken as already looked up, so opening an inventory does not offer completions of its own name
        // and warn that it duplicates itself - see NameSuggestions.StartsAt.
        _inventoryNameSuggestions.StartsAt(inventory.Name);
        _isShowingWhatIsStored = true;
        IsPrivate = inventory.IsPrivate;
        _isShowingWhatIsStored = false;

        // A private inventory is offered to nobody: the server holds no readable copy to hand over,
        // which is what makes it private - the same line Orbit.Web's editor draws.
        if (inventory is { ServerId: { } serverId, IsPrivate: false })
        {
            Share.Describes(
                SharedItemKind.Inventory, serverId, inventory.Name,
                inventory.AccessLevel == "CanEdit" ? null : inventory.OwnerUserId);
        }
        else
        {
            Share.OffersNothing();
        }

        HasHistory = (await _inventories.GetHistoryOfAsync(_localId, cancellationToken)).Count > 0;
        _items = inventory.Items;
        _arrivals = inventory.ItemArrivals;
        // What this shelf's restock list asks for, and when - see RestockListSettingsPanel.
        await RestockList.ShowFor(inventory.ServerId, cancellationToken);

        // Sealed with a key this device cannot open - see TaskListDetailViewModel for the same guard
        // and why saving one anyway is worse than not offering to.
        if (inventory.IsSealed)
        {
            IsReadOnly = true;
            ReadOnlyReason = await _privateContent.HasKeyAsync(cancellationToken)
                ? _translations["This inventory was sealed with an encryption key this account no longer has."]
                : _translations["This inventory is private. Unlock this device's encryption key to read it."];
            IsCopyOffered = false;
        }
        else
        {
            IsReadOnly = !await _inventories.CanEditAsync(_localId, cancellationToken);
            // Said in the same words the row on the list before it used - being told it cannot be
            // changed, without being told why, leaves a screen that simply looks broken.
            ReadOnlyReason = OfflineEditExplanation.For(
                inventory, OfflineEditPolicy.Evaluate(inventory, _networkStatus), hasUnsentChanges: false,
                _translations);
            // <inheritdoc cref="Tasks.TaskListDetailViewModel"/> - a copy is for editing offline what
            // could be edited online.
            IsCopyOffered = IsReadOnly && inventory.CopyOfLocalId is null && SharedItemAccess.AllowsEditing(inventory);
        }

        if (!IsReadOnly && inventory.ServerId is { } lockedServerId)
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
        Offer(ProductTypes, _anyProductType, item => [item.ProductType]);
        // An item can be filed under several now, so this reads across all of them rather than taking
        // one per row - see InventoryItem.Categories.
        Offer(Categories, _anyCategory, item => item.AllCategories);

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

    private void Offer(
        ObservableCollection<string> options, string forAny, Func<InventoryItemRequest, IEnumerable<string>> of)
    {
        var onTheShelf = _items
            .SelectMany(item => of(item).Select(value => value.Trim()))
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
            Items.Add(InventoryItemRow.From(item, _translations, _pointedAtProductId, ArrivalOf(item)));
        }

        OnPropertyChanged(nameof(PointedAtRow));
        OnPropertyChanged(nameof(IsNarrowed));
        OnPropertyChanged(nameof(FilterNote));
        OnPropertyChanged(nameof(EmptyMessage));
    }

    /// <summary>
    /// When this batch arrived, or null for one this phone has queued and no server has accepted - it
    /// has no id yet, or none this shelf was told about.
    /// </summary>
    private DateTimeOffset? ArrivalOf(InventoryItemRequest item)
        => item.Id is { } id && _arrivals.TryGetValue(id, out var arrived) ? arrived : null;

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
                await ShowStoredInventoryAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Status = _translations["Saved on this phone - it will sync later"];
        }
    }

    partial void OnBeingEditedChanged(InventoryItemEditor? value)
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
    /// Inventory names this account already has, offered under the name field. Its own instance rather
    /// than the one above: the name and the quick-add box are on screen together, and one instance
    /// serves one field - see NameSuggestions.Takes.
    /// </summary>
    public NameSuggestions InventoryNameSuggestions => _inventoryNameSuggestions;

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

    partial void OnNameChanged(string value) => InventoryNameSuggestions.ShowFor(value);

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

    /// <inheritdoc cref="Notes.NoteDetailViewModel.IsCopyOffered"/>
    [ObservableProperty]
    private bool _isCopyOffered;

    /// <inheritdoc cref="Notes.NoteDetailViewModel.CopyForEditingAsync"/>
    [RelayCommand]
    private async Task CopyForEditingAsync(CancellationToken cancellationToken)
    {
        if (await _inventories.CopyForEditingAsync(_localId, cancellationToken) is not { } copy)
        {
            return;
        }

        IsCopyOffered = false;
        _navigator.ShowInventory(copy.LocalId);
    }

    /// <inheritdoc cref="Notes.NoteDetailViewModel.DeclineCopy"/>
    [RelayCommand]
    private void DeclineCopy() => IsCopyOffered = false;

    /// <summary>
    /// Whether anything was ever copied from this - what puts its history within reach. Hidden until
    /// there is one, because most things have none and a permanent link to an empty window is clutter.
    /// </summary>
    [ObservableProperty]
    private bool _hasHistory;

    /// <summary>This thing's own history, opened from this thing - see CopyHistoryViewModel.</summary>
    [RelayCommand]
    private void GoToHistory() => _navigator.ShowCopyHistory(CopyKind.Inventory, _localId);

    /// <summary>How this inventory's restock list is built - see <see cref="RestockListSettingsPanel"/>.</summary>
    public RestockListSettingsPanel RestockList { get; }
}

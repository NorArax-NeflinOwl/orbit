using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Orbit.Core.Inventories;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Screens.Sharing;
using Orbit.Core.Suggestions;
using Orbit.Mobile.Screens.Suggestions;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One task list and its items. Every change is written to the local database first and queued from
/// there, so ticking something off works with no connection and the screen never waits on a request.
///
/// The offline policy is enforced by the store rather than here - see <see cref="LocalWriteOutcome"/> -
/// so this screen's job is to say what happened, not to be the only thing standing between a shared
/// list and an edit that should not have been queued.
/// </summary>
public sealed partial class TaskListDetailViewModel : ObservableObject
{
    private readonly LocalTaskListRepository _taskLists;

    /// <summary>Making or correcting the appointment a Calendar entry carries - see <see cref="EntryAppointment"/>.</summary>
    private readonly EntryAppointment _entryAppointment;

    /// <summary>The shelf an Inventory errand is about - see <see cref="ShelfCorrection"/>.</summary>
    private readonly ShelfCorrection _shelfCorrection;
    private readonly IPlacePicker _placePicker;
    private readonly TaskListSynchronizer _synchronizer;
    private readonly TasksClient _tasksClient;
    private readonly IChecklistReadingStore _reading;
    private readonly EditLock _editLock;
    private readonly Translations _translations;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;
    private readonly PrivateContentSealer _privateContent;
    private readonly NameSuggestions _nameSuggestions;
    private readonly NameSuggestions _titleSuggestions;
    private readonly IScreenNavigator _navigator;

    private Guid _localId;
    private Guid? _serverId;
    private IReadOnlyList<TaskItemDto> _items = [];

    /// <summary>
    /// The appointment behind each Calendar entry that already has one, by the id the entry carries.
    /// Read from this phone's own copy of the calendar, so opening an entry offline still shows when it
    /// happens rather than an empty form that would overwrite it on save.
    /// </summary>
    private IReadOnlyDictionary<Guid, CalendarEventDetailsDto> _appointments =
        new Dictionary<Guid, CalendarEventDetailsDto>();

    [ObservableProperty]
    private string _title = string.Empty;

    /// <summary>
    /// What the list is about, under its title - the same field Orbit.Web draws as one control with the
    /// title above it. Empty for a private list, where the server keeps none.
    /// </summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>What the description was when it was last shown or saved - see CommitDescription.</summary>
    private string _savedDescription = string.Empty;

    /// <summary>
    /// What order the entries are read in. The list itself is unchanged by it - this is how one person
    /// reads one list on one device, remembered for it - so what is saved always goes back in the order
    /// the list was arranged in. See ChecklistReading.
    /// </summary>
    [ObservableProperty]
    private ChecklistOrder _itemOrder;

    /// <summary>
    /// Whether moving an entry up or down means anything. It does not while the list is being read in
    /// some other order: "up" would move it in an arrangement nobody can see, and the entry would stay
    /// exactly where it is on screen.
    /// </summary>
    public bool CanBeRearranged => ItemOrder == ChecklistOrder.AsArranged;

    /// <summary>
    /// Whether a description is worth offering at all: a private list keeps none, because a
    /// description stored in the clear would say in the open what the name is sealed to hide.
    /// </summary>
    public bool IsNotPrivate => !IsPrivate;

    [ObservableProperty]
    private string _newItemDescription = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    /// <inheritdoc cref="Inventory.InventoryDetailViewModel.BeingEdited"/>
    [ObservableProperty]
    private TaskItemEditor? _beingEdited;

    public bool IsEditingItem => BeingEdited is not null;

    public bool IsShowingList => BeingEdited is null;

    /// <summary>
    /// The entry whose tick raised the restock question, or null when nothing is being asked. Shaped
    /// like <see cref="BeingEdited"/> for the same reason: the row is what answering needs, so holding
    /// it is what "being asked" means.
    /// </summary>
    [ObservableProperty]
    private TaskItemRow? _restockTickBeingAsked;

    public bool IsAskingToFinishRestocking => RestockTickBeingAsked is not null;

    /// <summary>
    /// The entry whose tick raised the question of the lists it stands for, or null when nothing is
    /// being asked - see <see cref="AskAboutTheListsBehind"/>.
    /// </summary>
    [ObservableProperty]
    private TaskItemRow? _linkedTickBeingAsked;

    public bool IsAskingAboutTheListsBehind => LinkedTickBeingAsked is not null;

    /// <summary>
    /// The lists the entry being asked about stands for and this phone can open, in the order it names
    /// them. A list this phone has not got is named in the question and left out of these: it is
    /// somewhere to go only if there is somewhere to go.
    /// </summary>
    public ObservableCollection<TaskItemReference> ListsBehindTheEntry { get; } = [];

    /// <summary>What the question says: every list this entry is waiting on, named.</summary>
    [ObservableProperty]
    private string _listsBehindTheEntryQuestion = string.Empty;

    public TaskListDetailViewModel(
        LocalTaskListRepository taskLists, TaskListSynchronizer synchronizer, Translations translations,
        TimeProvider timeProvider, SharePanel share, IScreenNavigator navigator,
        TasksClient tasksClient, EditLock editLock,
        INetworkStatus networkStatus, StockCheckPanel stockCheck,
        EntryAppointment appointments, ShelfCorrection shelfCorrection, IPlacePicker placePicker,
        PrivateContentSealer privateContent, NameSuggestions nameSuggestions,
        NameSuggestions titleSuggestions, IChecklistReadingStore reading)
    {
        _reading = reading;
        _taskLists = taskLists;
        _entryAppointment = appointments;
        _shelfCorrection = shelfCorrection;
        _placePicker = placePicker;
        _synchronizer = synchronizer;
        _translations = translations;
        _timeProvider = timeProvider;
        Share = share;
        _navigator = navigator;
        _tasksClient = tasksClient;
        _editLock = editLock;
        _editLock.Changed += (_, _) => ShowWhoElseIsEditing();
        _networkStatus = networkStatus;
        _privateContent = privateContent;
        _nameSuggestions = nameSuggestions;
        _titleSuggestions = titleSuggestions;
        _titleSuggestions.Offers(NameSuggestionKind.TaskListTitle);
        _titleSuggestions.Takes = title => Title = title;
        OfferNamesToTheQuickAddBox();
        StockCheck = stockCheck;
        // Generating an inventory or pointing at a different one changes the list itself, so the screen
        // re-reads rather than letting the panel and the list drift apart.
        StockCheck.Changed += (_, _) => LoadCommand.Execute(null);

        Priorities = PriorityChoice.All(translations);
        _chosenPriority = PriorityChoice.For(nameof(Orbit.Core.Abstractions.ItemPriority.Normal), translations);
    }

    public ObservableCollection<TaskItemRow> Items { get; } = [];

    /// <summary>
    /// Whether this list gathers the lists its items link to rather than holding work of its own -
    /// Orbit.Web's "Group list". It is also what makes the stock check worth asking, and the phone had
    /// no way to set it, so a list made here could never be one.
    /// </summary>
    [ObservableProperty]
    private bool _isGroup;

    /// <summary>
    /// Only its owner may ever read this list, and the server never can. Orbit.Web's task editor has
    /// had the checkbox all along; the phone carried the flag without being able to set one - see
    /// PrivateContentSealer.
    /// </summary>
    [ObservableProperty]
    private bool _isPrivate;

    /// <summary>
    /// How much this list matters - Orbit.Web has had the same three choices on its task editor all
    /// along, and the phone could sort by them without ever being able to see or set one.
    /// </summary>
    public IReadOnlyList<PriorityChoice> Priorities { get; }

    [ObservableProperty]
    private PriorityChoice _chosenPriority;

    /// <summary>
    /// What a save writes down, kept beside the picker rather than read off it. The generated setter
    /// hands the new value to the hook below, and a save started from there must not have to guess
    /// whether the property itself has caught up yet - it had not, and every priority chosen on the
    /// phone was saved as the one it replaced.
    /// </summary>
    private string _priority = nameof(Orbit.Core.Abstractions.ItemPriority.Normal);

    /// <summary>"Can this be done?" - see StockCheckPanel. Only a group list is asked.</summary>
    public StockCheckPanel StockCheck { get; }

    /// <summary>Offering this to somebody else - see SharePanel.</summary>
    public SharePanel Share { get; }

    public bool HasStatus => Status.Length > 0;

    public bool CanEdit => !IsReadOnly;

    public void Open(Guid localId) => _localId = localId;

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        await ShowStoredListAsync(cancellationToken);
        await SettleFinishedErrandsAsync(cancellationToken);
    }

    /// <summary>
    /// Settles anything already crossed off on a restock list: each finished errand fills its shelf item
    /// and leaves the list. Asked on opening rather than on ticking, which is where Orbit.Web asks it -
    /// and it has to be asked here too, or the same list settles itself in a browser and quietly does
    /// not on a phone, which is the one thing two clients on one account must never do.
    ///
    /// Best effort. A settle that could not be asked for leaves the list exactly as it was, which is
    /// readable and correct-looking; saying so over a checklist somebody came here to use would be
    /// noise about something they did not ask for.
    /// </summary>
    private async Task SettleFinishedErrandsAsync(CancellationToken cancellationToken)
    {
        if (_serverId is not { } serverId || !RestockTaskNaming.IsManagedTitle(Title))
        {
            return;
        }

        try
        {
            if (await _tasksClient.ReconcileRestockingAsync(serverId, cancellationToken) == 0)
            {
                return;
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            return;
        }

        // The server moved the errands, so the list is pulled back rather than rewritten from here.
        await SynchroniseAsync(cancellationToken);
        await ShowStoredListAsync(cancellationToken);
    }

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private Task AddItemAsync(CancellationToken cancellationToken)
    {
        var description = NewItemDescription.Trim();
        NewItemDescription = string.Empty;

        // Both channels start at Push, as Orbit.Web's new-entry defaults do. "None" would be a quieter
        // default in name only: nothing on this screen says a channel is off, so an entry added here
        // would go overdue in silence and look like push was broken rather than switched off.
        return SaveAsync(
            [.. _items, new TaskItemDto(Guid.Empty, description, null, false, null, "Push", false, "Push", new TimeOnly(9, 0))],
            cancellationToken);
    }

    private bool CanAddItem => NewItemDescription.Trim().Length > 0;

    /// <summary>Opens one entry's details - when it is due, and what it says when it is late.</summary>
    [RelayCommand]
    private void EditItem(TaskItemRow? row)
    {
        if (row is not null && CanEdit)
        {
            BeingEdited = TaskItemEditor.For(
                row.Item, _translations, AppointmentFor(row.Item), LinkTargets, _nameSuggestions,
                ShelfProductFor(row.Item),
                // For an entry that becomes an errand while the form is open: the fields for a product
                // this shelf has not got appear with the choice rather than after a save.
                ShelfForSomethingNew);

            // Where the entry can go depends on what it stands for, and that changes while the form is
            // open - see MoveTargetsForTheEntry.
            BeingEdited.PropertyChanged += (_, changed) =>
            {
                if (changed.PropertyName == nameof(TaskItemEditor.LinkableTaskListsLeft))
                {
                    SayWhereTheEntryCanGo();
                }
            };

            SayWhereTheEntryCanGo();
        }
    }

    /// <summary>The other lists this entry could go to - see <see cref="TaskListChoice"/>.</summary>
    public ObservableCollection<TaskListChoice> MoveTargets { get; } = [];

    /// <summary>
    /// Lists an entry can be made to stand for. A group list gathers other lists, and it gathers them
    /// through its entries pointing at them - so a phone that could turn "group list" on and not point
    /// an entry anywhere could make a group that gathered nothing. Orbit.Web offers the same picker.
    /// </summary>
    public ObservableCollection<TaskListChoice> LinkTargets { get; } = [];

    /// <summary>
    /// Moving is a change to two lists, which only the server can make - so it is offered only for an
    /// entry the server already knows about, and only while there is somebody to ask.
    ///
    /// "Has an id" used to be the test for that, and stopped being one when this phone started naming
    /// its own entries (see LocalTaskListRepository.WithIdentity): an entry written a second ago now has
    /// an id too. What actually says the server has seen it is that nothing about this list is still
    /// waiting to be pushed - after a successful sync the queue is empty and everything on the list is
    /// known, and while anything is queued this entry may be the thing that is queued.
    /// </summary>
    public bool CanMoveItem
        => CanEdit
            && _networkStatus.IsOnline
            && MoveTargetsForTheEntry.Count > 0
            && !_isWaitingToBePushed
            && BeingEdited?.ToDto().Id is { } itemId && itemId != Guid.Empty;

    /// <summary>Whether this list has changes the server has not been told about - see CanMoveItem.</summary>
    private bool _isWaitingToBePushed;

    private async Task ShowWhetherAnythingIsQueuedAsync(CancellationToken cancellationToken)
    {
        _isWaitingToBePushed = (await _taskLists.GetPendingLocalIdsAsync(cancellationToken)).Contains(_localId);
        OnPropertyChanged(nameof(CanMoveItem));
    }

    [RelayCommand]
    private async Task MoveItemAsync(TaskListChoice? target, CancellationToken cancellationToken)
    {
        if (target?.ServerId is not { } targetServerId
            || BeingEdited?.ToDto().Id is not { } itemId
            || await _taskLists.FindAsync(_localId, cancellationToken) is not { ServerId: { } sourceServerId })
        {
            return;
        }

        BeingEdited = null;

        // Anything queued goes first: the server is about to be asked to rearrange these two lists, and
        // it should be rearranging what the phone last said, not a version behind.
        await SynchroniseAsync(cancellationToken);
        var outcome = await _tasksClient.MoveItemAsync(sourceServerId, itemId, targetServerId, cancellationToken);

        // Then again, to bring both lists back as the server now has them - said after the sync, which
        // clears the status of its own.
        await SynchroniseAsync(cancellationToken);
        await ShowStoredListAsync(cancellationToken);

        Status = outcome switch
        {
            WriteOutcome.Applied => _translations.Format("Moved to {0}.", target.Name),
            // A rule about the entry itself, so trying again would only fail again - say so rather than
            // asking for another go. See WriteOutcome.Rejected.
            WriteOutcome.Rejected => _translations["That move isn't allowed."],
            _ => _translations["Couldn't move it. Try again."]
        };
    }

    /// <summary>
    /// The lists this screen's entries stand for, by the server id they name - so an entry that points
    /// at one can offer the way into it. Read from this phone's own copy, like everything else a row
    /// points at.
    /// </summary>
    private IReadOnlyDictionary<Guid, LocalTaskList> _linkedTaskLists = new Dictionary<Guid, LocalTaskList>();

    private void SayWhereTheEntryCanGo()
    {
        OnPropertyChanged(nameof(MoveTargetsForTheEntry));
        OnPropertyChanged(nameof(CanMoveItem));
    }

    /// <summary>
    /// Where the entry being edited could actually go: any other list, except one it already stands for.
    /// An entry cannot link to the list it belongs to, and the server refuses such a move outright - so
    /// it is left out rather than offered and then rejected, the way the linking picker already leaves
    /// out what the entry stands for. Orbit.Web's editor draws the same distinction.
    /// </summary>
    public IReadOnlyList<TaskListChoice> MoveTargetsForTheEntry
        => BeingEdited is not { LinkedTaskLists.Count: > 0 } editor
            ? [.. MoveTargets]
            : [.. MoveTargets.Where(target =>
                editor.LinkedTaskLists.All(linked => linked.ServerId != target.ServerId))];

    private async Task ShowWhereItCanGoAsync(CancellationToken cancellationToken)
    {
        var others = await _taskLists.GetAllAsync(cancellationToken);

        _linkedTaskLists = others
            .Where(list => list.ServerId is not null)
            .ToDictionary(list => list.ServerId!.Value);

        MoveTargets.Clear();

        // The same lists offered for a different act: moving an entry sends it away, pointing at one
        // leaves it here and makes it stand for that list - which is what a group list is made of.
        LinkTargets.Clear();
        LinkTargets.Add(TaskListChoice.NoList(_translations));

        // By the id an entry names a list with, so a row standing for one can say which and open it.
        var byServerId = new Dictionary<Guid, TaskItemReference>();
        foreach (var other in others.Where(list => list.LocalId != _localId && list.ServerId is not null))
        {
            MoveTargets.Add(new TaskListChoice(other.ServerId!.Value, other.Title));
            LinkTargets.Add(new TaskListChoice(other.ServerId!.Value, other.Title));
            byServerId[other.ServerId!.Value] = new(
                other.Title, other.LocalId, TaskItemReferenceTarget.TaskList);
        }

        _listsByServerId = byServerId;
    }

    /// <summary>
    /// The lists this phone holds, by the id entries point at them with. Read with the list for the
    /// reason the move targets are: an entry standing for another list has to be able to name it the
    /// moment somebody presses its box, not after a round trip.
    /// </summary>
    private IReadOnlyDictionary<Guid, TaskItemReference> _listsByServerId =
        new Dictionary<Guid, TaskItemReference>();

    /// <summary>
    /// Read with the list rather than when an entry is opened, for the reason Orbit.Web's editor gives:
    /// the picker offering them has to be filled before anybody opens an entry, not after. The local
    /// store rather than the API, so it is there with no connection like everything else on this screen.
    /// </summary>
    private async Task ShowWhatItCanBeTiedToAsync(CancellationToken cancellationToken)
    {
        _appointments = await _entryAppointment.KnownByServerIdAsync(cancellationToken);
    }

    /// <summary>
    /// The appointment an entry already has, or null when saving it will make one. Null is also the
    /// answer for an entry whose event this phone has not synced yet, which opens the form on today
    /// rather than on nothing - the event itself is not lost, since the id still travels untouched.
    /// </summary>
    /// <summary>
    /// The product behind each Inventory entry, by the shelf item's id, together with the inventory it
    /// sits on. Read from this phone's own copy rather than asked for: the whole point of the link is
    /// that the row already knows which product it means, and a correction has to be possible offline
    /// the same as every other edit on this screen.
    /// </summary>
    private IReadOnlyDictionary<Guid, ShelfProductLocation> _shelfProducts =
        new Dictionary<Guid, ShelfProductLocation>();

    /// <summary>Where one product lives, so a change made here knows which inventory to go back to.</summary>
    private sealed record ShelfProductLocation(Guid InventoryLocalId, string InventoryName, InventoryItemRequest Product);

    private async Task ShowWhatItsErrandsAreAboutAsync(CancellationToken cancellationToken)
    {
        var shelves = await _shelfCorrection.ShelvesAsync(cancellationToken);
        var byProductId = new Dictionary<Guid, ShelfProductLocation>();
        foreach (var inventory in shelves)
        {
            // A product still waiting to be pushed has no id yet, so nothing can be pointing at it.
            foreach (var product in inventory.Items.Where(product => product.Id is not null))
            {
                byProductId[product.Id!.Value] = new(inventory.LocalId, inventory.Name, product);
            }
        }

        _shelfProducts = byProductId;
        _theListsOwnShelf = _linkedInventoryId is { } inventoryId
            ? shelves.FirstOrDefault(inventory => inventory.ServerId == inventoryId)
            : null;
    }

    /// <summary>
    /// The product an errand is about, ready to edit, or null when this phone has not got it - a
    /// inventory somebody stopped sharing, or one not synced yet. The entry still opens either way.
    /// </summary>
    private TaskItemShelfProduct? ShelfProductFor(TaskItemDto item)
    {
        if (item.LinkedInventoryItemId is { } productId && _shelfProducts.TryGetValue(productId, out var found))
        {
            return TaskItemShelfProduct.For(
                found.InventoryLocalId, found.InventoryName, found.Product, _translations);
        }

        // Nothing on the shelf answers to this entry yet, and the list says which shelf it is measured
        // against - so the entry describes something to put there rather than showing an empty form.
        // Orbit.Web's editor offers the same two cases through the same fields.
        return item.Kind == nameof(TaskItemKind.Inventory) ? ShelfForSomethingNew() : null;
    }

    /// <summary>
    /// A form for a product the list's own shelf has not got yet, or null when it is measured against
    /// no shelf - see TaskItemEditor.ShelfForSomethingNew.
    /// </summary>
    private TaskItemShelfProduct? ShelfForSomethingNew()
        => _theListsOwnShelf is { } shelf
            ? TaskItemShelfProduct.ForSomethingNotOnTheShelfYet(shelf.LocalId, shelf.Name, _translations)
            : null;

    /// <summary>
    /// The inventory this list is measured against, as this phone holds it. Null when the list names
    /// none, or names one this phone has not got - there is nothing to put a product on either way.
    /// </summary>
    private LocalInventory? _theListsOwnShelf;

    /// <summary>The server's id for that inventory, as the list carries it.</summary>
    private Guid? _linkedInventoryId;

    /// <summary>
    /// Every list other than this one that is asking for the same product, by that product's id. Worked
    /// out from this phone's own lists rather than asked for - Orbit.Web asks its server, which is the
    /// difference between a browser and something that has to work on a train.
    /// </summary>
    private IReadOnlyDictionary<Guid, IReadOnlyList<TaskItemReference>> _alsoAskedForBy =
        new Dictionary<Guid, IReadOnlyList<TaskItemReference>>();

    private async Task ShowWhoElseIsAskingAsync(CancellationToken cancellationToken)
    {
        var byProductId = new Dictionary<Guid, List<TaskItemReference>>();
        foreach (var list in await _taskLists.GetAllAsync(cancellationToken))
        {
            if (list.LocalId == _localId)
            {
                continue;
            }

            foreach (var productId in list.Items
                .Where(item => item.Kind == nameof(TaskItemKind.Inventory))
                .Select(item => item.LinkedInventoryItemId)
                .OfType<Guid>()
                .Distinct())
            {
                byProductId.TryAdd(productId, []);
                byProductId[productId].Add(new(
                    // The title as stored, which is how every other screen on this phone shows one.
                    _translations.Format("also on {0}", list.Title),
                    list.LocalId,
                    TaskItemReferenceTarget.TaskList));
            }
        }

        _alsoAskedForBy = byProductId.ToDictionary(
            pair => pair.Key, pair => (IReadOnlyList<TaskItemReference>)pair.Value);
    }

    /// <summary>
    /// Where an inventory errand points: the shelf it is about first, then every other list asking for
    /// the same product. Both are somewhere to go rather than something to read - see TaskItemReference.
    /// </summary>
    private IReadOnlyList<TaskItemReference> ReferencesFor(TaskItemDto item)
    {
        // An entry that stands for other lists is a way into them, and was a dead end here: a group
        // list is nothing but such entries, so its screen offered no way to the work it gathers. The
        // browser draws the whole tree as stacked cards; a phone has room for one list at a time, so
        // this says which lists the entry stands for and lets the reader walk into each.
        if (item.AllLinkedTaskListIds.Count > 0)
        {
            return [.. item.AllLinkedTaskListIds
                // A list this reader cannot see, or one this phone has not pulled yet: there is nothing
                // to open, and a chip that leads nowhere is worse than no chip.
                .Select(linkedId => _linkedTaskLists.GetValueOrDefault(linkedId))
                .Where(linked => linked is not null)
                .Select(linked => new TaskItemReference(
                    _translations.Written(linked!.Title), linked.LocalId, TaskItemReferenceTarget.TaskList))];
        }

        if (item.Kind != nameof(TaskItemKind.Inventory) || item.LinkedInventoryItemId is not { } productId)
        {
            return [];
        }

        var references = new List<TaskItemReference>();
        if (_shelfProducts.TryGetValue(productId, out var shelf))
        {
            references.Add(new(
                _translations.Format("in {0}", shelf.InventoryName),
                shelf.InventoryLocalId,
                TaskItemReferenceTarget.Inventory,
                productId));
        }

        if (_alsoAskedForBy.TryGetValue(productId, out var elsewhere))
        {
            references.AddRange(elsewhere);
        }

        return references;
    }

    /// <summary>Opens what a reference points at, which is the whole reason it is shown.</summary>
    [RelayCommand]
    private void OpenReference(TaskItemReference? reference)
    {
        if (reference is null)
        {
            return;
        }

        if (reference.Target == TaskItemReferenceTarget.Inventory)
        {
            _navigator.ShowInventory(reference.LocalId, reference.ProductId);
            return;
        }

        _navigator.ShowTaskList(reference.LocalId);
    }

    private CalendarEventDetailsDto? AppointmentFor(TaskItemDto item)
        => item.LinkedCalendarEventId is { } eventId && _appointments.TryGetValue(eventId, out var details)
            ? details
            : _appointmentsWaitingToBeNamed.GetValueOrDefault(item.Description);

    /// <summary>
    /// Appointments made on this phone that the server has not named yet, by the entry they belong to.
    /// Without this an entry saved offline would reopen on an empty form, and the next save would make a
    /// second event rather than correcting the first - see PendingCalendarLink.
    /// </summary>
    private IReadOnlyDictionary<string, CalendarEventDetailsDto> _appointmentsWaitingToBeNamed =
        new Dictionary<string, CalendarEventDetailsDto>();

    private async Task ShowAppointmentsWaitingToBeNamedAsync(CancellationToken cancellationToken)
    {
        // Keyed on the words rather than the id, for the reason PendingCalendarLink gives: an entry
        // made offline has no id of its own yet.
        var waiting = new Dictionary<string, CalendarEventDetailsDto>();
        foreach (var item in _items.Where(item => item.Kind == nameof(TaskItemKind.Calendar)))
        {
            if (await _entryAppointment.FindWaitingForAsync(_localId, item.Description, cancellationToken) is { } pending)
            {
                waiting[item.Description] = pending.Details;
            }
        }

        _appointmentsWaitingToBeNamed = waiting;
    }

    [RelayCommand]
    private void CancelItemEdit() => BeingEdited = null;

    /// <summary>
    /// Points at where this entry happens on a map instead of typing it - see <see cref="IPlacePicker"/>,
    /// and Orbit.Web's "Show map" beside the same box. Nothing is written back until the reader confirms
    /// the pin: a stray tap on a map must not rewrite an address somebody typed.
    /// </summary>
    [RelayCommand]
    private async Task ShowMapAsync(CancellationToken cancellationToken)
    {
        if (BeingEdited is not { CanSayWhereItHappens: true } editor)
        {
            return;
        }

        var picked = await _placePicker.PickAsync(editor.Location, cancellationToken);
        if (picked.Outcome is PickedPlaceOutcome.Chosen)
        {
            // Where the pin was, whatever the box says: that is what the appointment actually stores,
            // and it was decided by the pin - see EventPlace.
            editor.LocationLatitude = picked.Latitude;
            editor.LocationLongitude = picked.Longitude;

            // The words, only into a box nobody has written in. "The back entrance" is the whole reason
            // that box is typed into, and replacing it with a street every time a pin is confirmed is
            // what made correcting it feel impossible. Orbit.Web's UseThePickedPlace draws this line in
            // the same place, and this screen's own calendar sibling has always drawn it.
            if (editor.Location.Trim().Length == 0)
            {
                editor.Location = picked.Address;
            }
        }
    }

    [RelayCommand]
    private async Task SaveItemAsync(CancellationToken cancellationToken)
    {
        if (BeingEdited is not { CanSave: true } editor)
        {
            return;
        }

        var edited = editor.ToDto();

        // What the appointment has to say, kept until the list is written: saving the list reports on
        // its own sync afterwards, and setting this first meant the reader never saw it.
        string? saidAboutTheAppointment = null;
        if (edited.Kind == nameof(TaskItemKind.Calendar))
        {
            var appointment = await _entryAppointment.SaveAsync(editor, edited, _localId, cancellationToken);

            // Nothing made the appointment, so the entry must not be saved pointing at one.
            if (appointment.Entry is not { } withItsAppointment)
            {
                Status = _translations[AppointmentRefusalMessage];
                return;
            }

            edited = withItsAppointment;
            saidAboutTheAppointment = appointment switch
            {
                { PlaceWasNotSaved: true } => _translations[PlaceNotFoundMessage],
                { Outcome: AppointmentOutcome.QueuedOnThisPhone } => _translations[AppointmentQueuedMessage],
                _ => null
            };
        }

        var shelf = editor.IsShelfEntry ? editor.Shelf : null;
        if (shelf is { Product.IsSomethingNew: true })
        {
            // The entry's own words are the product's name - the form asks everything except that, and
            // this is where the two are put together. Taken from what is being saved rather than from
            // what was opened, so renaming the entry in the same sitting names the product.
            shelf.Product.Name = edited.Description;
        }

        BeingEdited = null;
        await SaveAsync([.. _items.Select(item => item.Id == edited.Id ? edited : item)], cancellationToken);

        if (saidAboutTheAppointment is { } said)
        {
            Status = said;
        }

        if (shelf is not null)
        {
            await CorrectTheShelfAsync(shelf, cancellationToken);
        }
    }

    /// <summary>
    /// Writes a corrected product back to its shelf and says so when it could not - see
    /// <see cref="ShelfCorrection"/> for why the correction itself lives outside this screen.
    /// </summary>
    private async Task CorrectTheShelfAsync(TaskItemShelfProduct shelf, CancellationToken cancellationToken)
    {
        if (await _shelfCorrection.ApplyAsync(shelf, cancellationToken) is ShelfCorrectionOutcome.Refused)
        {
            Status = _translations[ShelfRefusalMessage];
            return;
        }

        await ShowWhatItsErrandsAreAboutAsync(cancellationToken);
    }

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string ShelfRefusalMessage =
        "The list was saved, but the shelf couldn't be updated. Open the inventory and check it.";

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string AppointmentRefusalMessage =
        "Somebody else can change this appointment, and Orbit can't be reached to check. It stays as it was until you're back online.";

    /// <inheritdoc cref="AppointmentRefusalMessage"/>
    private const string PlaceNotFoundMessage =
        "Saved, but that place could not be found - open the map and point at it to keep it.";

    /// <inheritdoc cref="AppointmentRefusalMessage"/>
    private const string AppointmentQueuedMessage =
        "Saved on this phone - the appointment reaches the calendar when you're back online.";

    /// <summary>
    /// Ticking off "Update stock levels" while errands are still open on the same list is either the end
    /// of a round of restocking or a tick on the standing reminder. Only the reader knows which, so they
    /// are asked - Orbit.Web asks the same question in the browser's confirm box.
    /// </summary>
    [RelayCommand]
    private Task ToggleItemAsync(TaskItemRow? row, CancellationToken cancellationToken)
    {
        if (row is null)
        {
            return Task.CompletedTask;
        }

        if (row.Item.AllLinkedTaskListIds.Count > 0)
        {
            AskAboutTheListsBehind(row);
            return Task.CompletedTask;
        }

        if (!row.IsCompleted && ClosesARestockRound(row))
        {
            RestockTickBeingAsked = row;
            return Task.CompletedTask;
        }

        return TickAsync(row, cancellationToken);
    }

    /// <summary>
    /// Answers a press on an entry that cannot be ticked here, by saying where the answer actually is:
    /// an entry standing for other lists is done when they are (see LinkedTaskCompletionResolver), so
    /// the press is taken, the lists are named, and going to one is offered. Orbit.Web asks the same
    /// question under the row it was asked about.
    /// </summary>
    private void AskAboutTheListsBehind(TaskItemRow row)
    {
        ListsBehindTheEntry.Clear();
        var names = new List<string>();
        foreach (var linkedTaskListId in row.Item.AllLinkedTaskListIds)
        {
            // A list this phone has not synced, or one somebody stopped sharing: named as "another
            // list" the way Orbit.Web names it, and not offered as somewhere to go - there is nothing
            // here to open.
            if (_listsByServerId.TryGetValue(linkedTaskListId, out var linked))
            {
                ListsBehindTheEntry.Add(linked);
                names.Add(linked.Label);
                continue;
            }

            names.Add(_translations["another list"]);
        }

        ListsBehindTheEntryQuestion = _translations.Format("This is done when {0} is.", string.Join(", ", names));
        LinkedTickBeingAsked = row;
    }

    /// <summary>"Yes" - the list the entry is waiting on, which is where the tick belongs.</summary>
    [RelayCommand]
    private void OpenTheListBehind(TaskItemReference? linked)
    {
        LinkedTickBeingAsked = null;
        if (linked is not null)
        {
            _navigator.ShowTaskList(linked.LocalId);
        }
    }

    /// <summary>"No" - the question dropped, and the entry left as it was.</summary>
    [RelayCommand]
    private void LeaveTheListBehind() => LinkedTickBeingAsked = null;

    private bool ClosesARestockRound(TaskItemRow row)
        => row.Description == RestockTaskNaming.UpdateStockReminderDescription
            && _items.Any(other => other.Id != row.Id && !other.IsCompleted);

    private Task TickAsync(TaskItemRow row, CancellationToken cancellationToken)
        => SaveAsync(
            _items.Select(item => item.Id == row.Id ? item with { IsCompleted = !item.IsCompleted } : item).ToList(),
            cancellationToken);

    /// <summary>"No" - the one tick the reader asked for, and the rest of the list left alone.</summary>
    [RelayCommand]
    private Task TickOnlyThisAsync(CancellationToken cancellationToken)
    {
        if (RestockTickBeingAsked is not { } row)
        {
            return Task.CompletedTask;
        }

        RestockTickBeingAsked = null;
        return TickAsync(row, cancellationToken);
    }

    /// <summary>
    /// "Yes, everything is done": every product in the inventory goes up to its minimum and the whole
    /// list is crossed off, the standing reminder included. Both are the server's doing, so the list is
    /// pulled back rather than written from here - see FinishRestockingCommandHandler.
    /// </summary>
    [RelayCommand]
    private async Task FinishRestockingAsync(CancellationToken cancellationToken)
    {
        RestockTickBeingAsked = null;
        if (_serverId is not { } serverId)
        {
            return;
        }

        int toppedUp;
        try
        {
            toppedUp = await _tasksClient.FinishRestockingAsync(serverId, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Status = _translations["Couldn't finish the restocking. Try again."];
            return;
        }

        // Said after the sync, which clears the status line on its way past.
        await SynchroniseAsync(cancellationToken);
        Status = _translations.Format("{0} brought up to their minimum.", toppedUp);
    }

    [RelayCommand]
    private Task RemoveItemAsync(TaskItemRow? row, CancellationToken cancellationToken)
        => row is null
            ? Task.CompletedTask
            : SaveAsync(_items.Where(item => item.Id != row.Id).ToList(), cancellationToken);

    /// <summary>
    /// Moves an entry one place. A checklist is read in order - first this, then that - and the phone
    /// could only add to the end of one, so an entry put down out of turn stayed out of turn. Orbit.Web
    /// drags them; a phone offers up and down, which is a target a thumb can hit in a scrolling list.
    ///
    /// Nothing is sent that is not sent anyway: the order a list is saved in is the order it is stored
    /// in, entry by entry - see TaskRepository.ToItemEntity - so arranging them here is arranging them
    /// everywhere.
    /// </summary>
    [RelayCommand]
    private Task MoveItemUpAsync(TaskItemRow? row, CancellationToken cancellationToken)
        => MoveItemAsync(row, by: -1, cancellationToken);

    [RelayCommand]
    private Task MoveItemDownAsync(TaskItemRow? row, CancellationToken cancellationToken)
        => MoveItemAsync(row, by: 1, cancellationToken);

    private Task MoveItemAsync(TaskItemRow? row, int by, CancellationToken cancellationToken)
    {
        var reordered = _items.ToList();
        var from = row is null ? -1 : reordered.FindIndex(item => item.Id == row.Id);
        var to = from + by;

        // The ends are where a list stops, not a failure: the first entry has nowhere above it.
        if (from < 0 || to < 0 || to >= reordered.Count)
        {
            return Task.CompletedTask;
        }

        (reordered[from], reordered[to]) = (reordered[to], reordered[from]);
        return SaveAsync(reordered, cancellationToken);
    }

    [RelayCommand]
    private async Task DeleteListAsync(CancellationToken cancellationToken)
    {
        var outcome = await _taskLists.DeleteAsync(_localId, cancellationToken);
        if (outcome.WasRefused())
        {
            Status = outcome.Explain(RefusalMessage, _translations);
            return;
        }

        await SynchroniseAsync(cancellationToken);
        _navigator.ShowTasks();
    }

    /// <summary>
    /// Writes the list down as it now stands. Named for what it does rather than for one of its
    /// callers: the store's update takes the whole list, so renaming it and making it a group list are
    /// the same write. Orbit.Web's task editor saves both the same way for the same reason.
    /// </summary>
    [RelayCommand]
    private Task SaveListAsync(CancellationToken cancellationToken) => SaveAsync(_items, cancellationToken);

    [RelayCommand]
    private void GoBack() => _navigator.ShowTasks();

    private async Task SaveAsync(IReadOnlyList<TaskItemDto> items, CancellationToken cancellationToken)
    {
        LocalWriteOutcome outcome;
        try
        {
            outcome = await _taskLists.UpdateAsync(
                _localId, new TaskListContent(Title, items, IsGroup, _priority, IsPrivate, Description),
                cancellationToken);
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

        await ShowStoredListAsync(cancellationToken);
        await SynchroniseAsync(cancellationToken);
    }

    private async Task ShowStoredListAsync(CancellationToken cancellationToken)
    {
        if (await _taskLists.FindAsync(_localId, cancellationToken) is not { } taskList)
        {
            _navigator.ShowTasks();
            return;
        }

        Title = taskList.Title;
        Description = taskList.Description;
        _savedDescription = taskList.Description;
        // Taken as already looked up, so opening a list does not offer completions of its own title and
        // warn that it duplicates itself - see NameSuggestions.StartsAt.
        _titleSuggestions.StartsAt(taskList.Title);
        _serverId = taskList.ServerId;
        // A private list is offered to nobody: the server holds no readable copy to hand over, which is
        // what makes it private - the same line Orbit.Web's editor draws.
        if (taskList is { ServerId: { } serverId, IsPrivate: false })
        {
            Share.Describes(
                SharedItemKind.TaskList, serverId, taskList.Title,
                taskList.AccessLevel == "CanEdit" ? null : taskList.OwnerUserId);
        }
        else
        {
            Share.OffersNothing();
        }

        _items = taskList.Items;
        // Which shelf this list is measured against, so an inventory entry on it knows where a product
        // it describes would go - see ShelfProductFor.
        _linkedInventoryId = taskList.LinkedInventoryId;
        _isShowingWhatIsStored = true;
        IsGroup = taskList.IsGroup;
        IsPrivate = taskList.IsPrivate;
        ChosenPriority = PriorityChoice.For(taskList.Priority, _translations);
        _isShowingWhatIsStored = false;
        await ShowWhereItCanGoAsync(cancellationToken);
        await ShowWhatItCanBeTiedToAsync(cancellationToken);
        // Both before the rows are built below: a row asks these two what it points at - see ReferencesFor.
        await ShowWhatItsErrandsAreAboutAsync(cancellationToken);
        await ShowWhoElseIsAskingAsync(cancellationToken);
        HasHistory = (await _taskLists.GetHistoryOfAsync(_localId, cancellationToken)).Count > 0;
        await ShowAppointmentsWaitingToBeNamedAsync(cancellationToken);
        await ShowWhetherAnythingIsQueuedAsync(cancellationToken);
        // Sealed with a key this device cannot open, so there is nothing here to change: the readable
        // fields are empty, and saving would replace the sealed list with an empty one.
        if (taskList.IsSealed)
        {
            IsReadOnly = true;
            ReadOnlyReason = await _privateContent.HasKeyAsync(cancellationToken)
                ? _translations["This list was sealed with an encryption key this account no longer has."]
                : _translations["This list is private. Unlock this device's encryption key to read it."];
            IsCopyOffered = false;
        }
        else
        {
            // Asked of the store rather than decided here, so the screen and the write agree by construction.
            IsReadOnly = !await _taskLists.CanEditAsync(_localId, cancellationToken);
            // Said in the same words the row on the list before it used - being told it cannot be
            // changed, without being told why, leaves a screen that simply looks broken.
            ReadOnlyReason = OfflineEditExplanation.For(
                taskList, OfflineEditPolicy.Evaluate(taskList, _networkStatus), hasUnsentChanges: false, _translations);
            // A copy is for editing offline what could be edited online, so there is nothing to take one
            // of when the share itself does not permit editing.
            IsCopyOffered = IsReadOnly && taskList.CopyOfLocalId is null && SharedItemAccess.AllowsEditing(taskList);
        }

        if (!IsReadOnly && taskList.ServerId is { } lockedServerId)
        {
            // Claimed for as long as this screen is open, so somebody editing the same thing on the web
            // is told rather than left to have their save refused - see EditLock.
            await _editLock.HoldAsync(_tasksClient, lockedServerId, cancellationToken);
            ShowWhoElseIsEditing();
        }

        // What was chosen for this list before anything is drawn from it, and without saving it back -
        // this is what was already chosen, not somebody choosing it again.
        _isShowingWhatIsStored = true;
        ItemOrder = _reading.Read(_localId).Order;
        _isShowingWhatIsStored = false;

        ShowItemsInChosenOrder(taskList.Items);

        await StockCheck.ShowAsync(taskList, cancellationToken);
    }

    /// <summary>
    /// The entries as this reader asked for them. Alphabetical is by what each entry says, which is how
    /// a shopping list gets read off in a shop; as arranged is the order the list was written in, which
    /// is what moving entries about sets. The same three orders Orbit.Web reads a checklist in.
    /// </summary>
    private void ShowItemsInChosenOrder(IReadOnlyList<TaskItemDto> items)
    {
        var inOrder = ItemOrder switch
        {
            ChecklistOrder.Alphabetical =>
                items.OrderBy(item => item.Description, StringComparer.CurrentCultureIgnoreCase),
            ChecklistOrder.UndoneFirst => items
                .OrderBy(item => item.IsCompleted)
                .ThenBy(item => item.Description, StringComparer.CurrentCultureIgnoreCase),
            _ => items.AsEnumerable()
        };

        Items.Clear();
        foreach (var item in inOrder)
        {
            Items.Add(TaskItemRow.From(
                item, _translations, _timeProvider.GetUtcNow(), ReferencesFor(item),
                _appointmentsWaitingToBeNamed.ContainsKey(item.Description)));
        }
    }

    partial void OnItemOrderChanged(ChecklistOrder value)
    {
        OnPropertyChanged(nameof(CanBeRearranged));
        ShowItemsInChosenOrder(_items);

        if (_isShowingWhatIsStored || _localId == Guid.Empty)
        {
            return;
        }

        // Read first: the stock check panel writes the same record - see ChecklistReading.
        _reading.Write(_localId, _reading.Read(_localId) with { Order = value });
    }

    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            Status = result.ReachedTheServer ? string.Empty : _translations["Saved on this phone - it will sync later"];

            // Re-read rather than keep what was shown before the sync. An entry added here has no server
            // id until the push comes back with one, and a later save built on the older copy would send
            // no id at all - so the server would mint a second entry and cut loose whatever pointed at
            // the first. See TaskItemRequest.Id.
            if (result.Received > 0)
            {
                await ShowStoredListAsync(cancellationToken);
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Status = _translations["Saved on this phone - it will sync later"];
        }
    }

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string RefusalMessage =
        "Somebody else can change this list, and Orbit can't be reached to check. It stays read-only until you're back online.";

    /// <inheritdoc cref="Inventory.InventoryDetailViewModel.Suggestions"/>
    public NameSuggestions Suggestions => _nameSuggestions;

    /// <summary>
    /// Titles this account already has, offered under the title field. Its own instance rather than the
    /// one above: both fields are on screen at once here, and one instance serves one field - see
    /// NameSuggestions.Takes. Orbit.Web arrives at the same place by putting one component per field.
    /// </summary>
    public NameSuggestions TitleSuggestions => _titleSuggestions;

    private void OfferNamesToTheQuickAddBox()
    {
        _nameSuggestions.Forget();
        _nameSuggestions.Offers(NameSuggestionKind.TaskItemDescription);
        _nameSuggestions.Takes = description => NewItemDescription = description;
    }

    /// <summary>True while the screen fills itself in, so loading does not look like a person choosing.</summary>
    private bool _isShowingWhatIsStored;

    partial void OnIsGroupChanged(bool value)
    {
        if (!_isShowingWhatIsStored)
        {
            SaveListCommand.Execute(null);
        }
    }

    /// <inheritdoc cref="OnIsGroupChanged"/>
    /// <summary>
    /// Saves the description once the reader has finished with it, and only if it changed. A box with no
    /// "done" key of its own is left by moving away from it, and the first thing typed here was lost
    /// exactly that way: everything else on this screen saves as it is chosen, and this saved on nothing.
    /// </summary>
    [RelayCommand]
    private Task CommitDescriptionAsync(CancellationToken cancellationToken)
    {
        if (Description == _savedDescription)
        {
            return Task.CompletedTask;
        }

        _savedDescription = Description;
        return SaveListCommand.ExecuteAsync(null);
    }

    partial void OnIsPrivateChanged(bool value)
    {
        OnPropertyChanged(nameof(IsNotPrivate));

        if (!_isShowingWhatIsStored)
        {
            SaveListCommand.Execute(null);
        }
    }

    /// <summary>Saved as soon as it is chosen, the way making a list a group list is.</summary>
    partial void OnChosenPriorityChanged(PriorityChoice value)
    {
        _priority = value.Value;
        if (!_isShowingWhatIsStored)
        {
            SaveListCommand.Execute(null);
        }
    }

    partial void OnBeingEditedChanged(TaskItemEditor? value)
    {
        // Nothing on offer once the form is gone, and the box above the list takes what is chosen
        // again - it is the field being typed into whenever no editor is.
        if (value is null)
        {
            OfferNamesToTheQuickAddBox();
        }

        OnPropertyChanged(nameof(IsEditingItem));
        OnPropertyChanged(nameof(IsShowingList));
        OnPropertyChanged(nameof(CanMoveItem));
        // What the entry stands for decides where it can go - see MoveTargetsForTheEntry.
        OnPropertyChanged(nameof(MoveTargetsForTheEntry));
    }

    partial void OnRestockTickBeingAskedChanged(TaskItemRow? value)
        => OnPropertyChanged(nameof(IsAskingToFinishRestocking));

    partial void OnLinkedTickBeingAskedChanged(TaskItemRow? value)
        => OnPropertyChanged(nameof(IsAskingAboutTheListsBehind));

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsReadOnlyChanged(bool value) => OnPropertyChanged(nameof(CanEdit));

    partial void OnNewItemDescriptionChanged(string value)
    {
        AddItemCommand.NotifyCanExecuteChanged();
        Suggestions.ShowFor(value);
    }

    partial void OnTitleChanged(string value) => TitleSuggestions.ShowFor(value);

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
        if (await _taskLists.CopyForEditingAsync(_localId, cancellationToken) is not { } copy)
        {
            return;
        }

        IsCopyOffered = false;
        _navigator.ShowTaskList(copy.LocalId);
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
    private void GoToHistory() => _navigator.ShowCopyHistory(CopyKind.TaskList, _localId);
}

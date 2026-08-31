using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Inventory;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Orbit.Core.Inventory;
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
    private readonly LocalCalendarEventRepository _calendarEvents;
    private readonly CalendarClient _calendarClient;
    private readonly LocalWarehouseRepository _warehouses;
    private readonly WarehouseSynchronizer _warehouseSynchronizer;
    private readonly InventoryClient _inventoryClient;
    private readonly IPlacePicker _placePicker;
    private readonly TaskListSynchronizer _synchronizer;
    private readonly TasksClient _tasksClient;
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

    [ObservableProperty]
    private string _newItemDescription = string.Empty;

    [ObservableProperty]
    private string _status = string.Empty;

    [ObservableProperty]
    private bool _isReadOnly;

    /// <inheritdoc cref="Inventory.WarehouseDetailViewModel.BeingEdited"/>
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

    public TaskListDetailViewModel(
        LocalTaskListRepository taskLists, TaskListSynchronizer synchronizer, Translations translations,
        TimeProvider timeProvider, SharePanel share, IScreenNavigator navigator,
        TasksClient tasksClient, CalendarClient calendarClient, EditLock editLock,
        INetworkStatus networkStatus, StockCheckPanel stockCheck,
        LocalCalendarEventRepository calendarEvents, LocalWarehouseRepository warehouses,
        WarehouseSynchronizer warehouseSynchronizer, InventoryClient inventoryClient,
        IPlacePicker placePicker,
        PrivateContentSealer privateContent, NameSuggestions nameSuggestions,
        NameSuggestions titleSuggestions)
    {
        _taskLists = taskLists;
        _calendarClient = calendarClient;
        _warehouses = warehouses;
        _warehouseSynchronizer = warehouseSynchronizer;
        _inventoryClient = inventoryClient;
        _calendarEvents = calendarEvents;
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
        // Generating a warehouse or pointing at a different one changes the list itself, so the screen
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
                ShelfProductFor(row.Item));
            MoveTarget = null;
            OnPropertyChanged(nameof(CanMoveItem));
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
    /// Choosing one moves the entry there and then, rather than waiting for this form's Save. The move
    /// is not part of the entry - it is a change to two lists - and Orbit.Web's editor does the same.
    /// </summary>
    [ObservableProperty]
    private TaskListChoice? _moveTarget;

    /// <summary>
    /// An entry added on this phone and not yet synced has no id the server would recognise, so there
    /// is nothing to move yet. Offline there is nobody to do the moving at all.
    /// </summary>
    public bool CanMoveItem
        => CanEdit
            && _networkStatus.IsOnline
            && MoveTargets.Count > 0
            && BeingEdited?.ToDto().Id is { } itemId && itemId != Guid.Empty;

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

        Status = outcome is WriteOutcome.Applied
            ? _translations.Format("Moved to {0}.", target.Name)
            : _translations["Couldn't move it. Try again."];
    }

    private async Task ShowWhereItCanGoAsync(CancellationToken cancellationToken)
    {
        var others = await _taskLists.GetAllAsync(cancellationToken);

        MoveTargets.Clear();

        // The same lists offered for a different act: moving an entry sends it away, pointing at one
        // leaves it here and makes it stand for that list - which is what a group list is made of.
        LinkTargets.Clear();
        LinkTargets.Add(TaskListChoice.NoList(_translations));

        foreach (var other in others.Where(list => list.LocalId != _localId && list.ServerId is not null))
        {
            MoveTargets.Add(new TaskListChoice(other.ServerId!.Value, other.Title));
            LinkTargets.Add(new TaskListChoice(other.ServerId!.Value, other.Title));
        }
    }

    /// <summary>
    /// Read with the list rather than when an entry is opened, for the reason Orbit.Web's editor gives:
    /// the picker offering them has to be filled before anybody opens an entry, not after. The local
    /// store rather than the API, so it is there with no connection like everything else on this screen.
    /// </summary>
    private async Task ShowWhatItCanBeTiedToAsync(CancellationToken cancellationToken)
    {
        var events = await _calendarEvents.GetAllAsync(cancellationToken);

        _appointments = events
            .Where(candidate => candidate.ServerId is not null)
            .ToDictionary(candidate => candidate.ServerId!.Value, candidate => candidate.Details);
    }

    /// <summary>
    /// The appointment an entry already has, or null when saving it will make one. Null is also the
    /// answer for an entry whose event this phone has not synced yet, which opens the form on today
    /// rather than on nothing - the event itself is not lost, since the id still travels untouched.
    /// </summary>
    /// <summary>
    /// The product behind each Inventory entry, by the shelf item's id, together with the warehouse it
    /// sits on. Read from this phone's own copy rather than asked for: the whole point of the link is
    /// that the row already knows which product it means, and a correction has to be possible offline
    /// the same as every other edit on this screen.
    /// </summary>
    private IReadOnlyDictionary<Guid, ShelfProductLocation> _shelfProducts =
        new Dictionary<Guid, ShelfProductLocation>();

    /// <summary>Where one product lives, so a change made here knows which warehouse to go back to.</summary>
    private sealed record ShelfProductLocation(Guid WarehouseLocalId, string WarehouseName, WarehouseItemDto Product);

    private async Task ShowWhatItsErrandsAreAboutAsync(CancellationToken cancellationToken)
    {
        var byProductId = new Dictionary<Guid, ShelfProductLocation>();
        foreach (var warehouse in await _warehouses.GetAllAsync(cancellationToken))
        {
            // A product still waiting to be pushed has no id yet, so nothing can be pointing at it.
            foreach (var product in warehouse.Items.Where(product => product.Id is not null))
            {
                byProductId[product.Id!.Value] = new(warehouse.LocalId, warehouse.Name, product);
            }
        }

        _shelfProducts = byProductId;
    }

    /// <summary>
    /// The product an errand is about, ready to edit, or null when this phone has not got it - a
    /// warehouse somebody stopped sharing, or one not synced yet. The entry still opens either way.
    /// </summary>
    private TaskItemShelfProduct? ShelfProductFor(TaskItemDto item)
        => item.LinkedInventoryItemId is { } productId && _shelfProducts.TryGetValue(productId, out var found)
            ? TaskItemShelfProduct.For(found.WarehouseLocalId, found.WarehouseName, found.Product, _translations)
            : null;

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
        if (item.Kind != nameof(TaskItemKind.Inventory) || item.LinkedInventoryItemId is not { } productId)
        {
            return [];
        }

        var references = new List<TaskItemReference>();
        if (_shelfProducts.TryGetValue(productId, out var shelf))
        {
            references.Add(new(
                _translations.Format("in {0}", shelf.WarehouseName),
                shelf.WarehouseLocalId,
                TaskItemReferenceTarget.Warehouse));
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

        if (reference.Target == TaskItemReferenceTarget.Warehouse)
        {
            _navigator.ShowWarehouse(reference.LocalId);
            return;
        }

        _navigator.ShowTaskList(reference.LocalId);
    }

    private CalendarEventDetailsDto? AppointmentFor(TaskItemDto item)
        => item.LinkedCalendarEventId is { } eventId && _appointments.TryGetValue(eventId, out var details)
            ? details
            : _appointmentsWaitingToBeNamed.GetValueOrDefault(item.Id);

    /// <summary>
    /// Appointments made on this phone that the server has not named yet, by the entry they belong to.
    /// Without this an entry saved offline would reopen on an empty form, and the next save would make a
    /// second event rather than correcting the first - see PendingCalendarLink.
    /// </summary>
    private IReadOnlyDictionary<Guid, CalendarEventDetailsDto> _appointmentsWaitingToBeNamed =
        new Dictionary<Guid, CalendarEventDetailsDto>();

    private async Task ShowAppointmentsWaitingToBeNamedAsync(CancellationToken cancellationToken)
    {
        var waiting = new Dictionary<Guid, CalendarEventDetailsDto>();
        foreach (var item in _items.Where(item => item.Kind == nameof(TaskItemKind.Calendar)))
        {
            if (await _calendarEvents.FindPendingForTaskItemAsync(item.Id, cancellationToken) is { } pending)
            {
                waiting[item.Id] = pending.Details;
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
            editor.Location = picked.Address;
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
        if (edited.Kind == nameof(TaskItemKind.Calendar))
        {
            if (await PutInTheCalendarAsync(editor, edited, cancellationToken) is not { } withItsAppointment)
            {
                return;
            }

            edited = withItsAppointment;
        }

        var shelf = editor.IsShelfEntry ? editor.Shelf : null;
        BeingEdited = null;
        await SaveAsync([.. _items.Select(item => item.Id == edited.Id ? edited : item)], cancellationToken);

        if (shelf is not null)
        {
            await SaveTheShelfAsync(shelf, cancellationToken);
        }
    }

    /// <summary>
    /// Writes a corrected product back to the warehouse it lives on, then asks that warehouse to work
    /// out its restock list again - a corrected amount can settle an errand or raise one, and a list
    /// still saying the old thing makes the correction look like it did not take.
    ///
    /// After the task list is saved rather than before, and it does not stop the save if it fails: the
    /// shelf is a second thing this screen touches, not the thing it is for. That is the order Orbit.Web
    /// settles on too, and the opposite of the calendar's - an appointment has to exist before the entry
    /// can name it, while a product already exists and is only being corrected.
    /// </summary>
    private async Task SaveTheShelfAsync(TaskItemShelfProduct shelf, CancellationToken cancellationToken)
    {
        if (await _warehouses.FindAsync(shelf.WarehouseLocalId, cancellationToken) is not { } warehouse)
        {
            return;
        }

        var corrected = shelf.Product.ToDto();
        var outcome = await _warehouses.UpdateAsync(
            shelf.WarehouseLocalId,
            new WarehouseContent(
                warehouse.Name,
                [.. warehouse.Items.Select(product => product.Id == corrected.Id ? corrected : product)],
                warehouse.IsPrivate),
            cancellationToken);

        if (outcome is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations[ShelfRefusalMessage];
            return;
        }

        // Pushed here rather than left for whenever somebody next opens the warehouse: the correction is
        // to a shelf this screen is not otherwise about, so nothing else would carry it up, and a
        // restock list rebuilt before the new amount arrives would be rebuilt from the old one.
        await _warehouseSynchronizer.SynchroniseAsync(cancellationToken);
        await RefreshTheRestockListAsync(warehouse.ServerId, cancellationToken);
        await ShowWhatItsErrandsAreAboutAsync(cancellationToken);
    }

    /// <summary>
    /// Best effort, and deliberately quiet: the correction is already saved on this phone and on its way
    /// up, and a restock list that is one sync behind rights itself. Saying "couldn't reach Orbit" about
    /// a change that did land would be the wrong thing to tell somebody.
    /// </summary>
    private async Task RefreshTheRestockListAsync(Guid? warehouseServerId, CancellationToken cancellationToken)
    {
        if (warehouseServerId is not { } serverId)
        {
            return;
        }

        try
        {
            await _inventoryClient.RefreshRestockListAsync(serverId, cancellationToken);
        }
        catch (HttpRequestException)
        {
        }
    }

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string ShelfRefusalMessage =
        "The list was saved, but the shelf couldn't be updated. Open the warehouse and check it.";

    /// <summary>
    /// Brings a Calendar entry's appointment into being, or into step, and hands back the entry carrying
    /// whatever id it should. Before the list is written rather than after, so there is no window where
    /// the entry exists and the appointment does not - the order Orbit.Web's SaveTheCalendarAsync
    /// settles on.
    ///
    /// Online this goes straight to the server, which names the event and lets the entry carry that name
    /// immediately. Offline it writes the event to this phone's own calendar and remembers the pairing
    /// (see PendingCalendarLink): the entry carries no server id yet, and gets one when the calendar
    /// syncs. Both are real appointments - the difference is only whether anybody else can see one yet,
    /// which is what the row's tag says.
    ///
    /// Null when even the local write was refused, so the caller stops rather than saving an entry that
    /// points at an appointment nobody made.
    /// </summary>
    private async Task<TaskItemDto?> PutInTheCalendarAsync(
        TaskItemEditor editor, TaskItemDto edited, CancellationToken cancellationToken)
    {
        var details = editor.Event.ToRequest(edited.Description);
        try
        {
            if (edited.LinkedCalendarEventId is { } eventId)
            {
                await _calendarClient.UpdateAsync(eventId, new UpdateCalendarEventRequest(details), cancellationToken);
                return edited;
            }

            return edited with
            {
                LinkedCalendarEventId = await _calendarClient.CreateAsync(
                    new CreateCalendarEventRequest(details), cancellationToken)
            };
        }
        catch (HttpRequestException)
        {
            return await PutInThisPhonesCalendarAsync(editor, edited, cancellationToken);
        }
    }

    /// <summary>
    /// The offline half: the appointment is written here and waits to be named. An entry already being
    /// corrected keeps the event it made earlier rather than making a second one - which is the whole
    /// reason the pairing is remembered rather than inferred.
    /// </summary>
    private async Task<TaskItemDto?> PutInThisPhonesCalendarAsync(
        TaskItemEditor editor, TaskItemDto edited, CancellationToken cancellationToken)
    {
        var details = editor.Event.ToDetails(edited.Description);
        if (await _calendarEvents.FindPendingForTaskItemAsync(edited.Id, cancellationToken) is { } waiting)
        {
            var outcome = await _calendarEvents.UpdateAsync(waiting.LocalId, details, cancellationToken);
            if (outcome is LocalWriteOutcome.RefusedWhileOffline)
            {
                Status = _translations[AppointmentRefusalMessage];
                return null;
            }

            Status = _translations[AppointmentQueuedMessage];
            return edited;
        }

        var created = await _calendarEvents.CreateAsync(details, cancellationToken);
        await _calendarEvents.RememberPendingLinkAsync(edited.Id, _localId, created.LocalId, cancellationToken);
        Status = _translations[AppointmentQueuedMessage];
        return edited;
    }

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string AppointmentRefusalMessage =
        "Somebody else can change this appointment, and Orbit can't be reached to check. It stays as it was until you're back online.";

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

        if (!row.IsCompleted && ClosesARestockRound(row))
        {
            RestockTickBeingAsked = row;
            return Task.CompletedTask;
        }

        return TickAsync(row, cancellationToken);
    }

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
    /// "Yes, everything is done": every product in the warehouse goes up to its minimum and the whole
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
        if (outcome is LocalWriteOutcome.RefusedWhileOffline)
        {
            Status = _translations[RefusalMessage];
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
                _localId, new TaskListContent(Title, items, IsGroup, _priority, IsPrivate), cancellationToken);
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
            Status = _translations[RefusalMessage];
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
        await ShowAppointmentsWaitingToBeNamedAsync(cancellationToken);
        // Sealed with a key this device cannot open, so there is nothing here to change: the readable
        // fields are empty, and saving would replace the sealed list with an empty one.
        if (taskList.IsSealed)
        {
            IsReadOnly = true;
            ReadOnlyReason = await _privateContent.HasKeyAsync(cancellationToken)
                ? _translations["This list was sealed with an encryption key this account no longer has."]
                : _translations["This list is private. Unlock this device's encryption key to read it."];
        }
        else
        {
            // Asked of the store rather than decided here, so the screen and the write agree by construction.
            IsReadOnly = !await _taskLists.CanEditAsync(_localId, cancellationToken);
            ReadOnlyReason = string.Empty;
        }

        if (!IsReadOnly && taskList.ServerId is { } lockedServerId)
        {
            // Claimed for as long as this screen is open, so somebody editing the same thing on the web
            // is told rather than left to have their save refused - see EditLock.
            await _editLock.HoldAsync(_tasksClient, lockedServerId, cancellationToken);
            ShowWhoElseIsEditing();
        }

        Items.Clear();
        foreach (var item in taskList.Items)
        {
            Items.Add(TaskItemRow.From(
                item, _translations, _timeProvider.GetUtcNow(), ReferencesFor(item),
                _appointmentsWaitingToBeNamed.ContainsKey(item.Id)));
        }

        await StockCheck.ShowAsync(taskList, cancellationToken);
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

    /// <inheritdoc cref="Inventory.WarehouseDetailViewModel.Suggestions"/>
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
    partial void OnIsPrivateChanged(bool value)
    {
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
    }

    partial void OnMoveTargetChanged(TaskListChoice? value)
    {
        if (value is not null)
        {
            MoveItemCommand.Execute(value);
        }
    }

    partial void OnRestockTickBeingAskedChanged(TaskItemRow? value)
        => OnPropertyChanged(nameof(IsAskingToFinishRestocking));

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
}

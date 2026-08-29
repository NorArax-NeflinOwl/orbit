using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Contracts.Tasks;
using Orbit.Core.Inventory;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Screens.Sharing;
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
    private readonly IPlacePicker _placePicker;
    private readonly TaskListSynchronizer _synchronizer;
    private readonly TasksClient _tasksClient;
    private readonly EditLock _editLock;
    private readonly Translations _translations;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;
    private readonly IScreenNavigator _navigator;

    private Guid _localId;
    private Guid? _serverId;
    private IReadOnlyList<TaskItemDto> _items = [];

    /// <summary>The events an entry could be tied to - see <see cref="CalendarEventChoice"/>.</summary>
    private IReadOnlyList<CalendarEventChoice> _linkableEvents = [];

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
        TasksClient tasksClient, EditLock editLock, INetworkStatus networkStatus, StockCheckPanel stockCheck,
        LocalCalendarEventRepository calendarEvents, IPlacePicker placePicker)
    {
        _taskLists = taskLists;
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
    private Task LoadAsync(CancellationToken cancellationToken) => ShowStoredListAsync(cancellationToken);

    [RelayCommand(CanExecute = nameof(CanAddItem))]
    private Task AddItemAsync(CancellationToken cancellationToken)
    {
        var description = NewItemDescription.Trim();
        NewItemDescription = string.Empty;

        return SaveAsync(
            [.. _items, new TaskItemDto(Guid.Empty, description, null, false, null, "None", false, "None", new TimeOnly(9, 0))],
            cancellationToken);
    }

    private bool CanAddItem => NewItemDescription.Trim().Length > 0;

    /// <summary>Opens one entry's details - when it is due, and what it says when it is late.</summary>
    [RelayCommand]
    private void EditItem(TaskItemRow? row)
    {
        if (row is not null && CanEdit)
        {
            BeingEdited = TaskItemEditor.For(row.Item, _translations, _linkableEvents);
            MoveTarget = null;
            OnPropertyChanged(nameof(CanMoveItem));
        }
    }

    /// <summary>The other lists this entry could go to - see <see cref="TaskListChoice"/>.</summary>
    public ObservableCollection<TaskListChoice> MoveTargets { get; } = [];

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
        if (target is null
            || BeingEdited?.ToDto().Id is not { } itemId
            || await _taskLists.FindAsync(_localId, cancellationToken) is not { ServerId: { } sourceServerId })
        {
            return;
        }

        BeingEdited = null;

        // Anything queued goes first: the server is about to be asked to rearrange these two lists, and
        // it should be rearranging what the phone last said, not a version behind.
        await SynchroniseAsync(cancellationToken);
        var outcome = await _tasksClient.MoveItemAsync(sourceServerId, itemId, target.ServerId, cancellationToken);

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
        foreach (var other in others.Where(list => list.LocalId != _localId && list.ServerId is not null))
        {
            MoveTargets.Add(new TaskListChoice(other.ServerId!.Value, other.Title));
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

        _linkableEvents = [.. events
            .Where(candidate => candidate.ServerId is not null)
            .Select(candidate => new CalendarEventChoice(
                candidate.ServerId, candidate.Details.Title, candidate.Details.Location?.Address ?? string.Empty))];
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
    private Task SaveItemAsync(CancellationToken cancellationToken)
    {
        if (BeingEdited is not { CanSave: true } editor)
        {
            return Task.CompletedTask;
        }

        var edited = editor.ToDto();
        BeingEdited = null;

        return SaveAsync([.. _items.Select(item => item.Id == edited.Id ? edited : item)], cancellationToken);
    }

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
        var outcome = await _taskLists.UpdateAsync(
            _localId, new TaskListContent(Title, items, IsGroup, _priority), cancellationToken);
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
        _serverId = taskList.ServerId;
        if (taskList.ServerId is { } serverId)
        {
            Share.Describes(
                SharedItemKind.TaskList, serverId, taskList.Title,
                taskList.AccessLevel == "CanEdit" ? null : taskList.OwnerUserId);
        }

        _items = taskList.Items;
        _isShowingWhatIsStored = true;
        IsGroup = taskList.IsGroup;
        ChosenPriority = PriorityChoice.For(taskList.Priority, _translations);
        _isShowingWhatIsStored = false;
        await ShowWhereItCanGoAsync(cancellationToken);
        await ShowWhatItCanBeTiedToAsync(cancellationToken);
        // Sealed with a key this phone has not got, so there is nothing here to change: the readable
        // fields arrive empty, and saving would send a private list with no ciphertext - which the
        // server refuses outright, and which would replace the sealed list with an empty one if it did
        // not. NoteDetailViewModel has always drawn this line; the list and the shelf did not.
        if (taskList.IsPrivate)
        {
            IsReadOnly = true;
            ReadOnlyReason = _translations[
                "This list is private, and its contents are sealed with a key this phone doesn't have."];
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
            Items.Add(TaskItemRow.From(item, _translations, _timeProvider.GetUtcNow()));
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

    /// <summary>True while the screen fills itself in, so loading does not look like a person choosing.</summary>
    private bool _isShowingWhatIsStored;

    partial void OnIsGroupChanged(bool value)
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

    partial void OnNewItemDescriptionChanged(string value) => AddItemCommand.NotifyCanExecuteChanged();

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

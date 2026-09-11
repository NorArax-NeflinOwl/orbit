using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Core.Abstractions;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Screens.Calendar;
using Orbit.Mobile.Screens.Location;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One entry of a task list on its own screen: what it is, when it is, and where - Orbit.Web's
/// TaskItemSummary page. This is what a deadline with a place opens as from the calendar, because a
/// checklist is the right landing for something to tick off and the wrong one for somewhere to get to.
///
/// Read from the phone's own store rather than asked for, as every other task screen is. Only the pin
/// needs the network, and only for an entry carrying an address of its own: an entry tied to an event
/// takes the place from the event, which is where the coordinates already live.
///
/// The one thing it changes is the tick. Written to this phone first and queued from there, like every
/// other task write - so crossing something off works with no connection - and refused by the store
/// rather than by this screen, which then says what the refusal was.
/// </summary>
public sealed partial class TaskItemSummaryViewModel : ObservableObject
{
    private readonly LocalTaskListRepository _taskLists;
    private readonly LocalCalendarEventRepository _calendarEvents;
    private readonly ChatRepository _contacts;
    private readonly PlaceSearch _places;
    private readonly TaskListSynchronizer _synchronizer;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    private Guid _taskListLocalId;
    private Guid _itemId;

    public TaskItemSummaryViewModel(
        LocalTaskListRepository taskLists, LocalCalendarEventRepository calendarEvents, PlaceSearch places,
        Translations translations, IScreenNavigator navigator, ChatRepository contacts,
        TaskListSynchronizer synchronizer)
    {
        _taskLists = taskLists;
        _calendarEvents = calendarEvents;
        _contacts = contacts;
        _places = places;
        _synchronizer = synchronizer;
        _translations = translations;
        _navigator = navigator;
    }

    /// <summary>What the entry says, which is the screen's own title.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>
    /// What the appointment says about itself, and who is coming. Both live on the event rather than on
    /// the entry - an entry that is an appointment keeps neither - so both are empty for an entry that
    /// is only a deadline. The browser shows the same two lines above the same map.
    /// </summary>
    [ObservableProperty]
    private string _appointmentDescription = string.Empty;

    [ObservableProperty]
    private string _guests = string.Empty;

    public bool HasAppointmentDescription => AppointmentDescription.Length > 0;

    public bool HasGuests => Guests.Length > 0;

    /// <summary>The list it sits on, so an entry read away from its list still says where it came from.</summary>
    [ObservableProperty]
    private string _taskListTitle = string.Empty;

    /// <summary>
    /// How far down its list the entry stands - "2 of 5", which is what the design's foot line says on
    /// the right. Counted from the list this screen already reads to find the entry in.
    /// </summary>
    [ObservableProperty]
    private string _position = string.Empty;

    /// <summary>Already in the reader's calendar, or "no date set" - the entry may have lost its date.</summary>
    [ObservableProperty]
    private string _when = string.Empty;

    [ObservableProperty]
    private string _where = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    /// <summary>Crossed out rather than ticked off - see Orbit.Core.Tasks.TaskItem.IsFailed.</summary>
    [ObservableProperty]
    private bool _isFailed;

    /// <summary>
    /// What the last tick came to, when it came to anything worth saying - a refusal, or a save this
    /// phone is still holding on to. Empty the rest of the time, which is most of it.
    /// </summary>
    [ObservableProperty]
    private string _status = string.Empty;

    public bool HasStatus => Status.Length > 0;

    /// <summary>Finished with, either way - what the entry's name is struck through for.</summary>
    public bool IsResolved => IsCompleted || IsFailed;

    /// <summary>
    /// Where the pin goes, or null when there is nowhere to put one. An address nobody can find stays
    /// as the words somebody typed rather than becoming a pin in the wrong country.
    /// </summary>
    [ObservableProperty]
    private MapPoint? _pin;

    public bool HasPin => Pin is not null;

    /// <summary>Said only when there was an address to look up and the lookup came back with nothing.</summary>
    public bool IsPlaceUnknown => Pin is null && Where != _translations["No place set"];

    public void Open(Guid taskListLocalId, Guid itemId)
    {
        _taskListLocalId = taskListLocalId;
        _itemId = itemId;
    }

    [RelayCommand]
    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        if (await _taskLists.FindAsync(_taskListLocalId, cancellationToken) is not { } taskList
            || taskList.Items.FirstOrDefault(candidate => candidate.Id == _itemId) is not { } item)
        {
            // Gone - crossed off and saved away, or the whole list deleted on another device.
            _navigator.ShowCalendar();
            return;
        }

        TaskListTitle = taskList.Title;
        Position = _translations.Format(
            "{0} of {1}",
            (taskList.Items.ToList().FindIndex(candidate => candidate.Id == _itemId) + 1).ToString(_translations.DisplayCulture),
            taskList.Items.Count.ToString(_translations.DisplayCulture));
        Description = item.Description;
        IsCompleted = item.IsCompleted;
        IsFailed = item.IsFailed;
        When = item.DueDateUtc is { } due
            ? due.LocalDateTime.ToString("g", _translations.DisplayCulture)
            : _translations["No date set"];

        await ShowWhereItIsAsync(item, cancellationToken);
    }

    /// <summary>
    /// The place, and where that is on a map. An entry tied to an event takes both from the event -
    /// that is the whole point of the tie, and the one place the address is kept, so there is nothing
    /// here to disagree with it. An entry with an address of its own has only the words, so they are
    /// looked up.
    /// </summary>
    private async Task ShowWhereItIsAsync(Orbit.Contracts.Tasks.TaskItemDto item, CancellationToken cancellationToken)
    {
        Pin = null;
        AppointmentDescription = string.Empty;
        Guests = string.Empty;

        var appointment = item.LinkedCalendarEventId is { } linkedEventId
            ? await FindEventAsync(linkedEventId, cancellationToken)
            : null;
        if (appointment is not null)
        {
            AppointmentDescription = appointment.Details.Description ?? string.Empty;
            Guests = await NameTheGuestsAsync(appointment.Details.Guests, cancellationToken);
        }

        if (appointment is { Details.Location: { } location })
        {
            Where = location.Address ?? string.Empty;
            Pin = new MapPoint(Description, location.Address, location.Latitude, location.Longitude, IsMine: false);
            return;
        }

        Where = item.Location.Length > 0 ? item.Location : _translations["No place set"];
        if (item.Location.Length == 0)
        {
            return;
        }

        if ((await _places.SearchAsync(item.Location, limit: 1, cancellationToken)).FirstOrDefault() is { } found)
        {
            Pin = new MapPoint(Description, item.Location, found.Latitude, found.Longitude, IsMine: false);
        }
    }

    /// <summary>
    /// The event this entry is tied to, matched by the id the tie is stored as. Null when this phone
    /// has not got that event, which leaves the entry's own words standing rather than the screen empty.
    /// </summary>
    private async Task<LocalCalendarEvent?> FindEventAsync(Guid serverId, CancellationToken cancellationToken)
        => (await _calendarEvents.GetAllAsync(cancellationToken))
            .FirstOrDefault(calendarEvent => calendarEvent.ServerId == serverId);

    /// <summary>
    /// Who is coming, named from this phone's contacts. Somebody invited from another device need not be
    /// one, and is named as "somebody else" rather than dropped: the count is the truth about how many
    /// are coming even when the names are not all known here.
    /// </summary>
    private async Task<string> NameTheGuestsAsync(
        IReadOnlyList<Guid> guests, CancellationToken cancellationToken)
    {
        if (guests.Count == 0)
        {
            return string.Empty;
        }

        var contacts = await _contacts.GetContactsAsync(cancellationToken);
        return string.Join(
            ", ",
            guests.Select(guestUserId =>
                contacts.FirstOrDefault(contact => contact.UserId == guestUserId)?.DisplayName
                    ?? _translations["Somebody else"]));
    }

    partial void OnAppointmentDescriptionChanged(string value) => OnPropertyChanged(nameof(HasAppointmentDescription));

    partial void OnGuestsChanged(string value) => OnPropertyChanged(nameof(HasGuests));

    /// <summary>
    /// Crosses this entry off, or takes the tick back - the light doing that belongs to the thing this
    /// screen reads. It used to belong to the list alone, so the one screen about this entry was the one
    /// place it could not be finished; Orbit.Web's own entry page ticks it the same way now.
    ///
    /// Written to this phone and queued from there, like every other task write. Whether it may be
    /// written at all is the store's answer rather than this screen's - see LocalWriteOutcome - so a
    /// read-only share and a shared list with no connection are refused in one place and said here.
    /// </summary>
    [RelayCommand]
    private async Task TickAsync(CancellationToken cancellationToken)
    {
        Status = string.Empty;
        if (await _taskLists.FindAsync(_taskListLocalId, cancellationToken) is not { } taskList
            || taskList.Items.FirstOrDefault(candidate => candidate.Id == _itemId) is not { } item)
        {
            // Gone underneath the reader, as LoadAsync answers the same case.
            _navigator.ShowCalendar();
            return;
        }

        // An entry standing for other lists is done when they are (see LinkedTaskCompletionResolver),
        // so there is nothing here to write: the press is taken and answered with where the tick
        // belongs, which is what the list screen and the browser both answer it with.
        if (item.AllLinkedTaskListIds.Count > 0)
        {
            Status = _translations.Format(
                "This is done when {0} is.", await NameTheListsBehindAsync(item, cancellationToken));
            return;
        }

        // One press moves to the next of the three answers - nothing, done, given up on. See TickState,
        // which is the same cycle the list screen and the browser follow.
        var next = Ticks.Read(item.IsCompleted, item.IsFailed).Next();

        // An entry waiting on unfinished work cannot be ticked, and only the tick is held back - see
        // TaskListSteps, which is the rule the server keeps whatever is sent to it.
        if (next == TickState.Completed && WhatItWaitsFor(item, taskList) is { Count: > 0 } steps)
        {
            Status = _translations.Format(
                "Waiting for {0}.", string.Join(", ", steps.Select(step => _translations.Written(step.Description))));
            return;
        }

        var items = taskList.Items
            .Select(candidate => candidate.Id == _itemId
                ? candidate with { IsCompleted = next.IsCompleted(), IsFailed = next.IsFailed() }
                : candidate)
            .ToList();

        LocalWriteOutcome outcome;
        try
        {
            outcome = await _taskLists.UpdateAsync(
                _taskListLocalId,
                new TaskListContent(
                    taskList.Title, items, taskList.IsGroup, taskList.Priority, taskList.IsPrivate,
                    taskList.Description),
                cancellationToken);
        }
        catch (EncryptionKeyLockedException)
        {
            // Sealing needs the account's own key, and this device has not got it - the same gate the
            // list screen sends the reader to for the same reason.
            _navigator.ShowChatKeyGate();
            return;
        }

        if (outcome.WasRefused())
        {
            Status = outcome.Explain(RefusalMessage, _translations);
            return;
        }

        IsCompleted = next.IsCompleted();
        IsFailed = next.IsFailed();
        await SynchroniseAsync(cancellationToken);
    }

    /// <summary>
    /// The entries of this list it is still waiting on. The same rule the server keeps: a step crossed
    /// out counts as not done, because that is what a cross says. See TaskListSteps.
    /// </summary>
    private static IReadOnlyList<Orbit.Contracts.Tasks.TaskItemDto> WhatItWaitsFor(
        Orbit.Contracts.Tasks.TaskItemDto item, LocalTaskList taskList)
        => [.. item.AllWaitsForTaskItemIds
            .Select(stepId => taskList.Items.FirstOrDefault(candidate => candidate.Id == stepId))
            .Where(step => step is { IsCompleted: false })
            .OfType<Orbit.Contracts.Tasks.TaskItemDto>()];

    /// <summary>The dictionary key, not the text itself - see <see cref="Translations"/>.</summary>
    private const string RefusalMessage =
        "Somebody else can change this list, and Orbit can't be reached to check. It stays read-only until you're back online.";

    /// <summary>
    /// The lists this entry stands for, named and joined. A list this phone has not synced, or one
    /// somebody stopped sharing, is "another list" - the same name Orbit.Web gives one it cannot name.
    /// </summary>
    private async Task<string> NameTheListsBehindAsync(
        Orbit.Contracts.Tasks.TaskItemDto item, CancellationToken cancellationToken)
    {
        var taskLists = await _taskLists.GetAllAsync(cancellationToken);
        return string.Join(
            ", ",
            item.AllLinkedTaskListIds.Select(linkedServerId =>
                taskLists.FirstOrDefault(candidate => candidate.ServerId == linkedServerId) is { } named
                    ? named.Title
                    : _translations["another list"]));
    }

    /// <summary>
    /// Sends what was just written, and says so when it could not go out - a tick that is only on this
    /// phone still counts, but a reader who has crossed something off on a shared list should know that
    /// nobody else can see it yet. See TaskListDetailViewModel, which says it in the same words.
    /// </summary>
    private async Task SynchroniseAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await _synchronizer.SynchroniseAsync(cancellationToken);
            Status = result.ReachedTheServer ? string.Empty : _translations["Saved on this phone - it will sync later"];
        }
        catch (Exception exception) when (exception is HttpRequestException or OperationCanceledException)
        {
            Status = _translations["Saved on this phone - it will sync later"];
        }
    }

    /// <summary>The list this entry is on, which is where the rest of it can be changed.</summary>
    [RelayCommand]
    private void ShowTaskList() => _navigator.ShowTaskList(_taskListLocalId);

    [RelayCommand]
    private void GoBack() => _navigator.ShowCalendar();

    partial void OnPinChanged(MapPoint? value)
    {
        OnPropertyChanged(nameof(HasPin));
        OnPropertyChanged(nameof(IsPlaceUnknown));
    }

    partial void OnWhereChanged(string value) => OnPropertyChanged(nameof(IsPlaceUnknown));

    partial void OnStatusChanged(string value) => OnPropertyChanged(nameof(HasStatus));

    partial void OnIsCompletedChanged(bool value) => OnPropertyChanged(nameof(IsResolved));

    partial void OnIsFailedChanged(bool value) => OnPropertyChanged(nameof(IsResolved));
}

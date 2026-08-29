using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Screens.Location;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One entry of a task list on its own screen: what it is, when it is, and where - Orbit.Web's
/// TaskItemSummary page. This is what a deadline with a place opens as from the calendar, because a
/// checklist is the right landing for something to tick off and the wrong one for somewhere to get to.
///
/// Read from the phone's own store rather than asked for, as every other task screen is. Only the pin
/// needs the network, and only for an entry carrying an address of its own: an entry tied to an event
/// takes the place from the event, which is where the coordinates already live.
/// </summary>
public sealed partial class TaskItemSummaryViewModel : ObservableObject
{
    private readonly LocalTaskListRepository _taskLists;
    private readonly LocalCalendarEventRepository _calendarEvents;
    private readonly PlaceSearch _places;
    private readonly Translations _translations;
    private readonly IScreenNavigator _navigator;

    private Guid _taskListLocalId;
    private Guid _itemId;

    public TaskItemSummaryViewModel(
        LocalTaskListRepository taskLists, LocalCalendarEventRepository calendarEvents, PlaceSearch places,
        Translations translations, IScreenNavigator navigator)
    {
        _taskLists = taskLists;
        _calendarEvents = calendarEvents;
        _places = places;
        _translations = translations;
        _navigator = navigator;
    }

    /// <summary>What the entry says, which is the screen's own title.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    /// <summary>The list it sits on, so an entry read away from its list still says where it came from.</summary>
    [ObservableProperty]
    private string _taskListTitle = string.Empty;

    /// <summary>Already in the reader's calendar, or "no date set" - the entry may have lost its date.</summary>
    [ObservableProperty]
    private string _when = string.Empty;

    [ObservableProperty]
    private string _where = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

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
        Description = item.Description;
        IsCompleted = item.IsCompleted;
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

        if (item.LinkedCalendarEventId is { } eventId
            && await FindEventAsync(eventId, cancellationToken) is { Details.Location: { } location })
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

    /// <summary>The list this entry is on, which is where it can be ticked off.</summary>
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
}

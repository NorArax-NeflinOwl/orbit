using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Orbit.Core.Notifications;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// One task-list entry while it is being edited. Everything <see cref="TaskItemDto"/> carries and the
/// phone could not reach: when it is due, what happens when it goes overdue, whether it says something
/// every day until it is done, and - for an entry that is somewhere to be rather than something to
/// fetch - what kind it is, where it happens and which event it belongs to.
///
/// A separate object from the DTO for the same reason as WarehouseItemEditor: a form holds half-typed
/// values, and "no due date" and "a date being picked" are different states the DTO cannot express.
/// </summary>
public sealed partial class TaskItemEditor : ObservableObject
{
    /// <summary>
    /// What the web's dropdown offers, in the same order and with the same wording - see
    /// NotificationChannelChoice. Taken in the factory rather than set afterwards: the picker reads it
    /// once, when the form appears, and a list handed over after that is never looked at.
    /// </summary>
    public IReadOnlyList<NotificationChannelChoice> Channels { get; private init; } = [];

    /// <summary>Bound to the picker, which needs a choice out of Channels rather than a string.</summary>
    public NotificationChannelChoice? ChosenOverdueNotificationChannel
    {
        get => NotificationChannelChoice.For(Channels, OverdueNotificationChannel);
        set
        {
            if (value is not null)
            {
                OverdueNotificationChannel = value.Value;
            }
        }
    }

    /// <summary>Bound to the picker, which needs a choice out of Channels rather than a string.</summary>
    public NotificationChannelChoice? ChosenDailyReminderNotificationChannel
    {
        get => NotificationChannelChoice.For(Channels, DailyReminderNotificationChannel);
        set
        {
            if (value is not null)
            {
                DailyReminderNotificationChannel = value.Value;
            }
        }
    }

    /// <summary>What an entry can be - see <see cref="TaskItemKindChoice"/>.</summary>
    public IReadOnlyList<TaskItemKindChoice> Kinds { get; private init; } = [];

    /// <summary>Bound to the picker, which needs a choice out of Kinds rather than a string.</summary>
    public TaskItemKindChoice? ChosenKind
    {
        get => TaskItemKindChoice.For(Kinds, Kind);
        set
        {
            if (value is not null)
            {
                Kind = value.Value;
            }
        }
    }

    /// <summary>
    /// The events this entry could be tied to, the first of them standing for none. Taken in the factory
    /// like the rest: the picker reads it once, when the form appears.
    /// </summary>
    public IReadOnlyList<CalendarEventChoice> CalendarEvents { get; private init; } = [];

    private readonly TaskItemDto _item;

    /// <summary>
    /// Only a calendar entry has somewhere to be, so the place and the tie to an event are shown for one
    /// and hidden for the rest - the same thing Orbit.Web's editor does with them.
    /// </summary>
    public bool IsCalendarEntry => Kind == nameof(TaskItemKind.Calendar);

    /// <summary>
    /// One place, not two: tied to an event, the event holds it, so the box gives way to what the event
    /// says rather than offering a second answer that could drift from the first.
    /// </summary>
    public bool CanSayWhereItHappens => IsCalendarEntry && ChosenCalendarEvent?.ServerId is null;

    public bool IsTiedToAnEvent => IsCalendarEntry && ChosenCalendarEvent?.ServerId is not null;

    public string WhereTheEventHappens
        => ChosenCalendarEvent?.Address is { Length: > 0 } address
            ? _translations.Format("Happens at {0}, which the event decides.", address)
            : _translations.Format(
                "Happens at {0}, which the event decides.", _translations["somewhere the event does not say"]);

    private readonly Translations _translations;

    [ObservableProperty]
    private string _kind = nameof(TaskItemKind.Checklist);

    [ObservableProperty]
    private CalendarEventChoice? _chosenCalendarEvent;

    /// <summary>Where a calendar entry happens, as somebody typed it - see TaskItemDto.Location.</summary>
    [ObservableProperty]
    private string _location = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private bool _hasDueDate;

    [ObservableProperty]
    private DateTime _dueDate = DateTime.Today;

    [ObservableProperty]
    private string _overdueNotificationChannel = nameof(NotificationChannel.None);

    [ObservableProperty]
    private bool _remindDaily;

    [ObservableProperty]
    private string _dailyReminderNotificationChannel = nameof(NotificationChannel.None);

    [ObservableProperty]
    private TimeSpan _dailyReminderTime = new(9, 0, 0);

    private TaskItemEditor(TaskItemDto item, Translations translations)
    {
        _item = item;
        _translations = translations;
    }

    /// <param name="events">
    /// What the calendar knows about, which the caller reads: the editor is handed the choices rather
    /// than reaching for a store, the same way it is handed the notification channels.
    /// </param>
    public static TaskItemEditor For(
        TaskItemDto item, Translations translations, IReadOnlyList<CalendarEventChoice> events)
    {
        var choices = new List<CalendarEventChoice> { CalendarEventChoice.NoEvent(translations) };
        choices.AddRange(events);

        return new(item, translations)
        {
            Channels = NotificationChannelChoice.All(translations),
            Kinds = TaskItemKindChoice.All(translations),
            CalendarEvents = choices,
            Kind = item.Kind,
            Location = item.Location,
            ChosenCalendarEvent = choices.FirstOrDefault(choice => choice.ServerId == item.LinkedCalendarEventId)
                ?? choices[0],
            Description = item.Description,
            HasDueDate = item.DueDateUtc is not null,
            DueDate = item.DueDateUtc?.LocalDateTime.Date ?? DateTime.Today,
            OverdueNotificationChannel = item.OverdueNotificationChannel,
            RemindDaily = item.RemindDaily,
            DailyReminderNotificationChannel = item.DailyReminderNotificationChannel,
            DailyReminderTime = item.DailyReminderTimeOfDay.ToTimeSpan()
        };
    }

    public bool CanSave => Description.Trim().Length > 0;

    /// <summary>
    /// Everything this screen does not show - the id, whether it is done, which list it points at -
    /// travels through untouched. An entry linked to an inventory item's restock task must come back
    /// linked.
    /// </summary>
    public TaskItemDto ToDto()
        => _item with
        {
            Description = Description.Trim(),
            // Converted rather than sent with the local offset the picker works in: Npgsql refuses a
            // DateTimeOffset with a non-zero offset for a "timestamp with time zone" column outright,
            // so a due date set here answered 500 and the queued save was given up on after five
            // tries - see CalendarViewModel, which has always converted for the same reason.
            DueDateUtc = HasDueDate
                ? new DateTimeOffset(DueDate.Date, TimeZoneInfo.Local.GetUtcOffset(DueDate.Date)).ToUniversalTime()
                : null,
            OverdueNotificationChannel = OverdueNotificationChannel,
            RemindDaily = RemindDaily,
            DailyReminderNotificationChannel = DailyReminderNotificationChannel,
            DailyReminderTimeOfDay = TimeOnly.FromTimeSpan(DailyReminderTime),
            Kind = Kind,
            Location = Location.Trim(),
            // Only a calendar entry can be tied to an event; anything else sends none, whatever the
            // picker last held. The same rule Orbit.Web's ToLinkedCalendarEventId applies.
            LinkedCalendarEventId = IsCalendarEntry ? ChosenCalendarEvent?.ServerId : null
        };

    partial void OnDescriptionChanged(string value) => OnPropertyChanged(nameof(CanSave));

    partial void OnKindChanged(string value) => SayWhatTheFormShows();

    partial void OnChosenCalendarEventChanged(CalendarEventChoice? value) => SayWhatTheFormShows();

    /// <summary>
    /// The three things that appear and disappear together: which of them is on screen depends on the
    /// kind and on whether an event was chosen, so both changes announce all three.
    /// </summary>
    private void SayWhatTheFormShows()
    {
        OnPropertyChanged(nameof(IsCalendarEntry));
        OnPropertyChanged(nameof(CanSayWhereItHappens));
        OnPropertyChanged(nameof(IsTiedToAnEvent));
        OnPropertyChanged(nameof(WhereTheEventHappens));
    }
}

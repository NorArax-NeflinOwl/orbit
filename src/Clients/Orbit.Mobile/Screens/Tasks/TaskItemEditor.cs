using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Orbit.Core.Notifications;
using Orbit.Core.Suggestions;
using Orbit.Mobile.Screens.Suggestions;

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

    /// <summary>
    /// Lists this entry can be made to stand for, the first of them being "none" - see
    /// TaskListChoice.NoList. A group list gathers other lists through entries that point at them.
    /// </summary>
    public IReadOnlyList<TaskListChoice> LinkableTaskLists { get; private init; } = [];

    /// <summary>
    /// The list this entry stands for, or the "none" choice. An entry that points somewhere is ticked
    /// by that list rather than by hand, which is why the tick is left to the list it names.
    /// </summary>
    [ObservableProperty]
    private TaskListChoice? _chosenLinkedTaskList;

    /// <summary>Nothing to point at is nothing to offer - a phone with one list needs no picker.</summary>
    public bool CanBeLinked => LinkableTaskLists.Count > 1;

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
    /// <inheritdoc cref="Inventory.WarehouseItemEditor.Suggestions"/>
    public NameSuggestions? Suggestions { get; private init; }

    public static TaskItemEditor For(
        TaskItemDto item, Translations translations, IReadOnlyList<CalendarEventChoice> events,
        IReadOnlyList<TaskListChoice> lists, NameSuggestions? suggestions = null)
    {
        var editor = Build(item, translations, events, lists, suggestions);
        if (suggestions is not null)
        {
            suggestions.Offers(NameSuggestionKind.TaskItemDescription);
            suggestions.StartsAt(editor.Description);
            suggestions.Takes = description => editor.Description = description;
        }

        return editor;
    }

    private static TaskItemEditor Build(
        TaskItemDto item, Translations translations, IReadOnlyList<CalendarEventChoice> events,
        IReadOnlyList<TaskListChoice> lists, NameSuggestions? suggestions)
    {
        var choices = new List<CalendarEventChoice> { CalendarEventChoice.NoEvent(translations) };
        choices.AddRange(events);

        return new(item, translations)
        {
            Suggestions = suggestions,
            Channels = NotificationChannelChoice.All(translations),
            Kinds = TaskItemKindChoice.All(translations),
            CalendarEvents = choices,
            LinkableTaskLists = lists,
            ChosenLinkedTaskList = lists.FirstOrDefault(choice => choice.ServerId == item.LinkedTaskListId)
                ?? lists.FirstOrDefault(),
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
            // Midnight reads as "never set". The wire carries a plain TimeOnly and so cannot say
            // "none"; an entry reminded daily at exactly 00:00 is far more likely to be one nobody
            // chose an hour for than one somebody wanted at midnight - and being asked is a smaller
            // cost than a reminder arriving while everybody is asleep. Orbit.Web reads it the same way.
            HasDailyReminderTime = !item.RemindDaily || item.DailyReminderTimeOfDay != default,
            DailyReminderTime = item.DailyReminderTimeOfDay == default
                ? DefaultReminderTime
                : item.DailyReminderTimeOfDay.ToTimeSpan()
        };
    }

    public bool CanSave => Description.Trim().Length > 0 && WhatIsMissing is null;

    /// <summary>
    /// What stops this entry being saved, or null when nothing does. Refused rather than quietly
    /// corrected: a daily reminder with no hour would be sent at midnight, and an hour nobody chose is
    /// worse than being asked for one.
    /// </summary>
    public string? WhatIsMissing
        => RemindDaily && !HasDailyReminderTime
            ? _translations["A daily reminder needs a time to arrive at."]
            : null;

    public bool HasSomethingMissing => WhatIsMissing is not null;

    /// <summary>
    /// Whether an hour has actually been chosen. False for an entry that arrived reminded daily at
    /// midnight, which is what "nobody chose one" looks like on the wire - see the factory.
    /// </summary>
    [ObservableProperty]
    private bool _hasDailyReminderTime = true;

    /// <summary>Nine in the morning, which is what the picker opens on when no hour has been chosen.</summary>
    private static readonly TimeSpan DefaultReminderTime = new(9, 0, 0);

    /// <summary>
    /// Everything this screen does not show - the id, whether it is done - travels through untouched.
    /// An entry linked to an inventory item's restock task must come back linked.
    /// </summary>
    public TaskItemDto ToDto()
        => _item with
        {
            LinkedTaskListId = ChosenLinkedTaskList?.ServerId,
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

    partial void OnDescriptionChanged(string value)
    {
        OnPropertyChanged(nameof(CanSave));
        Suggestions?.ShowFor(value);
    }

    /// <summary>Choosing an hour is what answers the refusal above, so the picker records that it was.</summary>
    partial void OnDailyReminderTimeChanged(TimeSpan value)
    {
        HasDailyReminderTime = true;
        SayWhetherItCanBeSaved();
    }

    /// <summary>
    /// Puts an hour on an entry that arrived without one, and opens the picker at it. Its own button
    /// rather than a picker showing nine o'clock from the start: a picker already showing an hour is
    /// one somebody can accept by not touching it, and accepting it would leave nothing recorded - the
    /// refusal would stand with no way through it. The browser has no such trap, its field being empty.
    /// </summary>
    [RelayCommand]
    private void ChooseAReminderTime()
    {
        DailyReminderTime = DefaultReminderTime;
        HasDailyReminderTime = true;
    }

    partial void OnRemindDailyChanged(bool value) => SayWhetherItCanBeSaved();

    partial void OnHasDailyReminderTimeChanged(bool value) => SayWhetherItCanBeSaved();

    private void SayWhetherItCanBeSaved()
    {
        OnPropertyChanged(nameof(WhatIsMissing));
        OnPropertyChanged(nameof(HasSomethingMissing));
        OnPropertyChanged(nameof(CanSave));
    }

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

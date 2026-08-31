using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Contracts.Calendar;
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
    /// The appointment this entry is - see <see cref="TaskItemEventForm"/>. Present on every entry, not
    /// only a Calendar one, so switching kinds back and forth does not lose what was typed.
    /// </summary>
    public TaskItemEventForm Event { get; private init; } = null!;

    /// <summary>
    /// The event this entry already has in the calendar, or null when saving it will make one. Carried
    /// through untouched: it is what tells an update from a creation, and nothing on this screen sets it.
    /// </summary>
    public Guid? LinkedCalendarEventId { get; private init; }

    /// <summary>
    /// The product an Inventory entry is an errand about - see <see cref="TaskItemShelfProduct"/>. Null
    /// for every other kind, and for an errand whose product this phone has not got: an entry tied to a
    /// warehouse somebody stopped sharing still opens, with the shelf half missing rather than the form.
    /// </summary>
    public TaskItemShelfProduct? Shelf { get; private init; }

    /// <summary>True when this entry is an errand about a product that can be corrected from here.</summary>
    public bool IsShelfEntry => Kind == nameof(TaskItemKind.Inventory) && Shelf is not null;

    /// <summary>
    /// Said before anything is changed: this form writes to a warehouse, not only to the list. Empty
    /// when there is no product behind the entry.
    /// </summary>
    public string WhereTheProductLives
        => Shelf is null
            ? string.Empty
            : _translations.Format(
                "On the shelf in {0}. Saving this list saves the change there too.", Shelf.WarehouseName);

    /// <summary>
    /// An Inventory entry with nothing behind it - one whose warehouse is gone, or not synced yet. Said
    /// rather than left as an empty form that looks broken, which is the line Orbit.Web draws too.
    /// </summary>
    public bool HasNoProductToEdit => Kind == nameof(TaskItemKind.Inventory) && Shelf is null;

    /// <inheritdoc cref="HasNoProductToEdit"/>
    public string NoProductMessage
        => _translations["This entry isn't tied to a product yet, so there is nothing to edit here."];

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
    /// Where a calendar entry happens, asked on the entry rather than on its appointment. The calendar's
    /// own location is coordinates first and an entry carries only a name, so the two are not the same
    /// field - which is why Orbit.Web leaves the name here and sends the event none.
    /// </summary>
    public bool CanSayWhereItHappens => IsCalendarEntry;

    private readonly Translations _translations;

    [ObservableProperty]
    private string _kind = nameof(TaskItemKind.Checklist);

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
        TaskItemDto item, Translations translations, CalendarEventDetailsDto? linkedEvent,
        IReadOnlyList<TaskListChoice> lists, NameSuggestions? suggestions = null,
        TaskItemShelfProduct? shelf = null)
    {
        var editor = Build(item, translations, linkedEvent, lists, suggestions, shelf);
        if (suggestions is not null)
        {
            suggestions.Offers(NameSuggestionKind.TaskItemDescription);
            suggestions.StartsAt(editor.Description);
            suggestions.Takes = description => editor.Description = description;
        }

        return editor;
    }

    private static TaskItemEditor Build(
        TaskItemDto item, Translations translations, CalendarEventDetailsDto? linkedEvent,
        IReadOnlyList<TaskListChoice> lists, NameSuggestions? suggestions, TaskItemShelfProduct? shelf)
    {
        var editor = new TaskItemEditor(item, translations)
        {
            Suggestions = suggestions,
            Channels = NotificationChannelChoice.All(translations),
            Kinds = TaskItemKindChoice.All(translations),
            Event = TaskItemEventForm.For(linkedEvent, translations),
            Shelf = shelf,
            LinkedCalendarEventId = item.LinkedCalendarEventId,
            LinkableTaskLists = lists,
            ChosenLinkedTaskList = lists.FirstOrDefault(choice => choice.ServerId == item.LinkedTaskListId)
                ?? lists.FirstOrDefault(),
            Kind = item.Kind,
            Location = item.Location,
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

        // The Save button answers to the appointment as well as to the entry - see TaskItemEventForm.
        editor.Event.Missing = editor.SayWhatTheFormShows;
        return editor;
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
            : IsCalendarEntry ? Event.WhatIsMissing
            : IsShelfEntry && !Shelf!.Product.CanSave
                ? _translations["This errand's product needs a name and an amount."]
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
            // Only a calendar entry keeps its appointment; anything else sends none, whatever it was
            // before. The same rule Orbit.Web's ToLinkedCalendarEventId applies. The id itself is
            // filled in by the screen when it puts the appointment in the calendar - see
            // TaskListDetailViewModel.PutAppointmentsInTheCalendarAsync.
            LinkedCalendarEventId = IsCalendarEntry ? LinkedCalendarEventId : null
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

    /// <summary>
    /// What appears and disappears together: the appointment's form and the place it happens are shown
    /// for a Calendar entry and hidden for the rest, and whether it can be saved depends on both halves.
    /// </summary>
    private void SayWhatTheFormShows()
    {
        OnPropertyChanged(nameof(IsCalendarEntry));
        OnPropertyChanged(nameof(CanSayWhereItHappens));
        OnPropertyChanged(nameof(IsShelfEntry));
        OnPropertyChanged(nameof(HasNoProductToEdit));
        OnPropertyChanged(nameof(WhereTheProductLives));
        OnPropertyChanged(nameof(WhatIsMissing));
        OnPropertyChanged(nameof(HasSomethingMissing));
        OnPropertyChanged(nameof(CanSave));
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using Orbit.Contracts.Calendar;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Mobile.Screens.Tasks;

/// <summary>
/// Where an appointment happens: what it is called, and where that is. Both or neither - an address
/// without a point cannot be stored on an event (see EventLocationDto), and a point with no name still
/// can.
///
/// Its own small type rather than three loose values threaded through two shapes: the name and the pair
/// of coordinates only ever travel together, and the rule about what makes a saveable place belongs
/// with them.
/// </summary>
public sealed record EventPlace(string Name = "", double? Latitude = null, double? Longitude = null)
{
    /// <summary>Nowhere - what an entry that says nothing about where it happens sends.</summary>
    public static EventPlace Nowhere { get; } = new();

    /// <summary>
    /// Whether this can actually be saved. A name somebody typed and nothing else cannot: the caller
    /// looks it up first, and says so when the lookup finds nothing - see EntryAppointment.
    /// </summary>
    public bool CanBeSaved => Latitude is not null && Longitude is not null;

    public EventLocationDto? ToDto()
        => Latitude is { } latitude && Longitude is { } longitude
            ? new EventLocationDto(Name.Trim() is { Length: > 0 } named ? named : null, latitude, longitude)
            : null;

    public EventLocationRequest? ToRequest()
        => Latitude is { } latitude && Longitude is { } longitude
            ? new EventLocationRequest(Name.Trim() is { Length: > 0 } named ? named : null, latitude, longitude)
            : null;
}

/// <summary>
/// The appointment a Calendar entry is, while somebody is writing it.
///
/// A Calendar entry no longer points at an event made elsewhere - it <b>is</b> the event, and saving the
/// list is what brings that event into being. This is the entry's half of Orbit.Web's event form, kept
/// as one object rather than nine fields on the editor so that switching an entry's kind back and forth
/// in one sitting does not lose what was typed.
///
/// The entry's own words are the event's title, which is why there is no title here.
/// </summary>
public sealed partial class TaskItemEventForm : ObservableObject
{
    private readonly Translations _translations;

    private TaskItemEventForm(Translations translations) => _translations = translations;

    /// <summary>
    /// The event as it stands, or an empty form starting today when the entry has none yet. Orbit.Web
    /// opens on no date at all and refuses to save until one is given; a phone has no empty date picker
    /// to offer, so it opens on today and the reader moves it.
    /// </summary>
    public static TaskItemEventForm For(CalendarEventDetailsDto? details, Translations translations)
    {
        var form = new TaskItemEventForm(translations)
        {
            Recurrences = RecurrenceChoice.All(translations),
            Reminders = ReminderChoice.All(translations)
        };

        if (details is null)
        {
            form.StartDate = DateTime.Today;
            form.EndDate = DateTime.Today;
            form.StartTime = DefaultStartTime;
            form.EndTime = DefaultEndTime;
            return form;
        }

        var start = details.StartUtc.ToLocalTime();
        var end = details.EndUtc.ToLocalTime();

        form.Description = details.Description ?? string.Empty;
        form.StartDate = start.Date;
        form.StartTime = start.TimeOfDay;
        // An all-day event ends at midnight after its last day, so the day a reader would name is the
        // one before - the same correction CalendarEventDetailViewModel makes when it opens one.
        form.EndDate = details.IsAllDay ? end.Date.AddDays(-1) : end.Date;
        form.EndTime = end.TimeOfDay;
        form.IsAllDay = details.IsAllDay;
        form.Colour = details.Color;
        form.ChosenRecurrence = form.Recurrences.FirstOrDefault(
            choice => choice.Value == details.Recurrence?.Frequency);
        form.ChosenReminder = details.ReminderMinutesBeforeStart.Count > 0
            ? ReminderChoice.For(details.ReminderMinutesBeforeStart[0], translations)
            : null;

        return form;
    }

    /// <summary>Nine to ten, the hour Orbit.Web's own new-event form opens on.</summary>
    private static readonly TimeSpan DefaultStartTime = new(9, 0, 0);

    private static readonly TimeSpan DefaultEndTime = new(10, 0, 0);

    /// <summary>How this entry can repeat. The "never" choice is the null one, so this list holds three.</summary>
    public IReadOnlyList<RecurrenceChoice> Recurrences { get; private init; } = [];

    public IReadOnlyList<ReminderChoice> Reminders { get; private init; } = [];

    /// <summary>What the appointment is about, beyond the entry's own words.</summary>
    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private DateTime _startDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _startTime = DefaultStartTime;

    [ObservableProperty]
    private DateTime _endDate = DateTime.Today;

    [ObservableProperty]
    private TimeSpan _endTime = DefaultEndTime;

    [ObservableProperty]
    private bool _isAllDay;

    /// <summary>Null for no colour, which is what most events are - see <see cref="EventColourChoice"/>.</summary>
    [ObservableProperty]
    private string? _colour;

    /// <summary>Null for an event that does not repeat, which the picker shows as its first choice.</summary>
    [ObservableProperty]
    private RecurrenceChoice? _chosenRecurrence;

    /// <summary>Null for no reminder.</summary>
    [ObservableProperty]
    private ReminderChoice? _chosenReminder;

    /// <summary>The swatches, with the one in force marked - rebuilt when the colour changes.</summary>
    public IReadOnlyList<EventColourChoice> Colours => EventColourChoice.All(Colour, _translations);

    /// <summary>
    /// What stops this appointment being saved, or null when nothing does. The two Orbit.Web refuses on
    /// as well: an entry that says when it ends but not when it starts is not an appointment, and one
    /// that ends before it starts is a typo rather than an intention.
    /// </summary>
    public string? WhatIsMissing
        => EndsBeforeItStarts ? _translations["This ends before it starts."] : null;

    private bool EndsBeforeItStarts => EndsAtUtc < StartsAtUtc;

    /// <summary>An all-day event has no hours to ask for, so the two time pickers go rather than sit dead.</summary>
    public bool ShowsTimes => !IsAllDay;

    public DateTimeOffset StartsAtUtc => ToUtc(StartDate.Date + (IsAllDay ? TimeSpan.Zero : StartTime));

    /// <summary>
    /// An all-day event runs to midnight after the last day it covers, so a one-day event ends a day
    /// after it starts. The same arithmetic the calendar's own editor does.
    /// </summary>
    public DateTimeOffset EndsAtUtc
        => ToUtc(IsAllDay ? EndDate.Date + TimeSpan.FromDays(1) : EndDate.Date + EndTime);

    /// <summary>
    /// The appointment in the shape this phone's own calendar stores, for one made with no connection -
    /// see PendingCalendarLink. The same fields as <see cref="ToRequest"/>; the two shapes differ only
    /// in the types the wire and the store each use for a place and a recurrence, and this one sets
    /// neither to anything but null.
    /// </summary>
    public CalendarEventDetailsDto ToDetails(string title, EventPlace place)
        => new(
            title,
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            place.ToDto(),
            Colour,
            StartsAtUtc,
            EndsAtUtc,
            IsAllDay,
            ChosenRecurrence is { } recurrence ? new RecurrenceDto(recurrence.Value, 1, null) : null,
            Guests: [],
            ChosenReminder is { } reminder ? [reminder.MinutesBefore] : [],
            ReminderNotificationChannel: "Push");

    /// <summary>
    /// The appointment in the shape the calendar takes. <paramref name="title"/> is the entry's own
    /// words: an entry and its appointment are one thing, so they cannot be named separately.
    /// </summary>
    public CalendarEventDetailsRequest ToRequest(string title, EventPlace place)
        => new(
            title,
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            // The place goes on the appointment, not on the entry. An entry tied to an event keeps no
            // place of its own - see Orbit.Core's TaskItem.WhereItHappens - and on a phone every
            // calendar entry is tied to one, so leaving the name on the entry threw it away on save.
            place.ToRequest(),
            Colour,
            StartsAtUtc,
            EndsAtUtc,
            IsAllDay,
            ChosenRecurrence is { } recurrence ? new RecurrenceRequest(recurrence.Value, 1, null) : null,
            Guests: [],
            ChosenReminder is { } reminder ? [reminder.MinutesBefore] : [],
            // Nobody is told an entry became an appointment, but the appointment itself still reminds -
            // the same channel Orbit.Web sets from its task editor.
            ReminderNotificationChannel: "Push");

    /// <summary>
    /// Converted rather than sent with the local offset the pickers work in: Npgsql refuses a
    /// DateTimeOffset with a non-zero offset for a "timestamp with time zone" column - see
    /// CalendarViewModel, which has always converted for the same reason.
    /// </summary>
    private static DateTimeOffset ToUtc(DateTime local)
        => new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();

    partial void OnColourChanged(string? value) => OnPropertyChanged(nameof(Colours));

    partial void OnIsAllDayChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowsTimes));
        SayWhatIsMissing();
    }

    partial void OnStartDateChanged(DateTime value) => SayWhatIsMissing();

    partial void OnStartTimeChanged(TimeSpan value) => SayWhatIsMissing();

    partial void OnEndDateChanged(DateTime value) => SayWhatIsMissing();

    partial void OnEndTimeChanged(TimeSpan value) => SayWhatIsMissing();

    private void SayWhatIsMissing()
    {
        OnPropertyChanged(nameof(WhatIsMissing));
        Missing?.Invoke();
    }

    /// <summary>
    /// Told to the editor above, so the Save button answers to the appointment as well as to the entry.
    /// A callback rather than an event: one editor holds one form, and an event left every form that had
    /// ever been open still listening.
    /// </summary>
    public Action? Missing { get; set; }
}

using Orbit.Contracts.Calendar;

namespace Orbit.Web.Services;

/// <summary>
/// Everything the event form holds, in the shape the form holds it: dates and times apart, the
/// recurrence rule as the boxes that describe it, the colour as a string the picker understands.
///
/// One model for two screens. The calendar's own editor edits an event directly; a task list's calendar
/// entry describes the event it will become, and saving the list is what makes it. They ask for exactly
/// the same things, so they share this and the control that draws it - see EventFields.razor. The title
/// is not here: the editor has a title box, and an entry's own words are its event's title.
/// </summary>
public sealed class EventFormModel
{
    public string Description { get; set; } = string.Empty;
    public string LocationAddress { get; set; } = string.Empty;
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }
    public string Color { get; set; } = "#4a90d9";

    /// <summary>ItemPriority by name, the way the API takes it - see CalendarEventDetailsDto.Priority.</summary>
    public string Priority { get; set; } = "Normal";

    public bool IsAllDay { get; set; }
    public DateOnly? StartDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public DateOnly? EndDate { get; set; }
    public TimeOnly? EndTime { get; set; }

    public bool IsRecurring { get; set; }
    public string RecurrenceFrequency { get; set; } = "Weekly";
    public int RecurrenceIntervalCount { get; set; } = 1;
    public DateOnly? RecurrenceUntil { get; set; }

    /// <summary>
    /// How many times in total, counting the first - the other way of saying when to stop. Null means
    /// no limit of this kind; see Orbit.Core.Calendar.EventRecurrence.OccurrenceCount.
    /// </summary>
    public int? RecurrenceCount { get; set; }

    public List<Guid> GuestUserIds { get; set; } = [];
    public List<ReminderRow> Reminders { get; set; } = [];

    /// <summary>
    /// Push by default, matching a task's overdue reminder and an inventory item's expiry. A new event
    /// used to arrive with no reminder and no channel, so an event created without opening either
    /// dropdown could never notify anyone - which reads, fairly, as reminders being broken.
    /// </summary>
    public string ReminderNotificationChannel { get; set; } = "Push";

    /// <summary>Say something when it begins, as well as beforehand - see CalendarEventDetails.NotifyAtStart.</summary>
    public bool NotifyAtStart { get; set; }

    /// <summary>
    /// Defaults a new event to start now and end an hour later. Start and end are each derived from a
    /// single timestamp (rather than combining DateTime.Today with a separately-computed time) so the
    /// end date rolls over correctly when starting late enough that adding an hour crosses midnight -
    /// otherwise the end date could default to the still-current day while the end time wraps to just
    /// after midnight, putting the end before the start.
    /// </summary>
    public EventFormModel()
    {
        var start = DateTime.Now;
        var end = start.AddHours(1);
        StartDate = DateOnly.FromDateTime(start);
        StartTime = TimeOnly.FromDateTime(start);
        EndDate = DateOnly.FromDateTime(end);
        EndTime = TimeOnly.FromDateTime(end);
    }

    /// <summary>
    /// The gap between start and end the last time both made sense - reapplied to the end whenever the
    /// start is moved past it, so an event keeps its length instead of collapsing to nothing or being
    /// left with its end before its beginning.
    /// </summary>
    private TimeSpan _lastKnownDuration = TimeSpan.FromHours(1);

    /// <summary>
    /// Drags the end along when the start moves past it, and otherwise remembers how long this event
    /// is. Called by the control after either of the start boxes changes.
    /// </summary>
    public void OnStartChanged()
    {
        if (StartInstant is not { } start)
        {
            return;
        }

        if (EndInstant is not { } end || start > end)
        {
            var corrected = start + _lastKnownDuration;
            EndDate = DateOnly.FromDateTime(corrected);
            EndTime = TimeOnly.FromDateTime(corrected);
            return;
        }

        _lastKnownDuration = end - start;
    }

    /// <summary>Keeps the remembered length current when the end is edited directly.</summary>
    public void OnEndChanged()
    {
        if (StartInstant is { } start && EndInstant is { } end && end >= start)
        {
            _lastKnownDuration = end - start;
        }
    }

    /// <summary>
    /// Whether the end has been put before the start. Asked while the form is being filled in rather
    /// than only when it is saved: moving the start drags the end along (see OnStartChanged), but an end
    /// edited directly is left where somebody put it, and finding out on Save that it was impossible is
    /// finding out too late.
    /// </summary>
    public bool EndsBeforeItStarts
        => StartInstant is { } start && EndInstant is { } end && end < start;

    /// <summary>
    /// Whether a repeat is told to stop before the first occurrence has even happened, which would make
    /// the rule describe nothing at all.
    /// </summary>
    public bool StopsRepeatingBeforeItStarts
        => IsRecurring && StartDate is { } start && RecurrenceUntil is { } until && until < start;

    private DateTime? StartInstant
        => StartDate is { } date ? date.ToDateTime(IsAllDay ? TimeOnly.MinValue : StartTime ?? TimeOnly.MinValue) : null;

    private DateTime? EndInstant
        => EndDate is { } date ? date.ToDateTime(IsAllDay ? TimeOnly.MinValue : EndTime ?? TimeOnly.MinValue) : null;

    public static EventFormModel FromDto(CalendarEventDetailsDto details)
        => new()
        {
            Description = details.Description ?? string.Empty,
            LocationAddress = details.Location?.Address ?? string.Empty,
            LocationLatitude = details.Location?.Latitude,
            LocationLongitude = details.Location?.Longitude,
            Color = string.IsNullOrWhiteSpace(details.Color) ? "#4a90d9" : details.Color,
            IsAllDay = details.IsAllDay,
            StartDate = DateOnly.FromDateTime(details.StartUtc.LocalDateTime),
            StartTime = TimeOnly.FromDateTime(details.StartUtc.LocalDateTime),
            EndDate = DateOnly.FromDateTime(details.EndUtc.LocalDateTime),
            EndTime = TimeOnly.FromDateTime(details.EndUtc.LocalDateTime),
            IsRecurring = details.Recurrence is not null,
            RecurrenceFrequency = details.Recurrence?.Frequency ?? "Weekly",
            RecurrenceIntervalCount = details.Recurrence?.IntervalCount ?? 1,
            RecurrenceUntil = details.Recurrence?.UntilUtc is { } until ? DateOnly.FromDateTime(until.LocalDateTime) : null,
            RecurrenceCount = details.Recurrence?.OccurrenceCount,
            GuestUserIds = [.. details.Guests],
            Reminders = [.. details.ReminderMinutesBeforeStart.Select(ReminderRow.FromMinutes)],
            ReminderNotificationChannel = details.ReminderNotificationChannel,
            NotifyAtStart = details.NotifyAtStart,
            Priority = details.Priority
        };

    /// <summary>
    /// The event as the API takes it. One mapping for both screens: the last three times a field went
    /// missing in this app, it was a second place building the same request and forgetting something.
    /// </summary>
    public CalendarEventDetailsRequest ToRequest(string title)
        => new(
            title,
            string.IsNullOrWhiteSpace(Description) ? null : Description,
            LocationLatitude is { } latitude && LocationLongitude is { } longitude
                ? new EventLocationRequest(
                    string.IsNullOrWhiteSpace(LocationAddress) ? null : LocationAddress.Trim(), latitude, longitude)
                : null,
            Color,
            ToDateTimeOffset(StartDate, StartTime, IsAllDay),
            ToDateTimeOffset(EndDate, EndTime, IsAllDay),
            IsAllDay,
            IsRecurring
                ? new RecurrenceRequest(
                    RecurrenceFrequency, RecurrenceIntervalCount, ToOptionalDateTimeOffset(RecurrenceUntil),
                    RecurrenceCount is { } count && count > 0 ? count : null)
                : null,
            GuestUserIds,
            [.. Reminders.Select(row => row.MinutesBeforeStart).Distinct().Order()],
            ReminderNotificationChannel,
            Priority,
            NotifyAtStart);

    /// <summary>
    /// Combines the separately-edited date and time-of-day into the single timestamp the API expects.
    /// All-day events are anchored to local midnight regardless of the time field.
    /// </summary>
    private static DateTimeOffset ToDateTimeOffset(DateOnly? date, TimeOnly? time, bool isAllDay)
    {
        var effectiveDate = date ?? DateOnly.FromDateTime(DateTime.Today);
        var effectiveTime = isAllDay ? TimeOnly.MinValue : time ?? TimeOnly.MinValue;
        return new DateTimeOffset(effectiveDate.ToDateTime(effectiveTime, DateTimeKind.Local)).ToUniversalTime();
    }

    private static DateTimeOffset? ToOptionalDateTimeOffset(DateOnly? date)
        => date is { } value ? new DateTimeOffset(value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Local)).ToUniversalTime() : null;
}

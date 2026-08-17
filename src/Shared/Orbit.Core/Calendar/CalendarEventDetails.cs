namespace Orbit.Core.Calendar;

/// <summary>
/// Everything about a calendar event other than its identity and bookkeeping timestamps - grouped
/// together since CalendarEvent.Create and CalendarEvent.Update both take and replace this whole set at
/// once.
/// </summary>
public sealed record CalendarEventDetails(
    string Title,
    string? Description,
    EventLocation? Location,
    string? Color,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsAllDay,
    EventRecurrence? Recurrence,
    IReadOnlyList<string> Guests,
    IReadOnlyList<int> ReminderMinutesBeforeStart);

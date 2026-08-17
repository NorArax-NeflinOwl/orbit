namespace Orbit.Contracts.Calendar;

public sealed record CalendarEventDetailsRequest(
    string Title,
    string? Description,
    EventLocationRequest? Location,
    string? Color,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsAllDay,
    RecurrenceRequest? Recurrence,
    IReadOnlyList<string> Guests,
    IReadOnlyList<int> ReminderMinutesBeforeStart);

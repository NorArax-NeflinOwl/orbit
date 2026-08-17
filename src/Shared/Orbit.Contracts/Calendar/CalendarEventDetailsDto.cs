namespace Orbit.Contracts.Calendar;

public sealed record CalendarEventDetailsDto(
    string Title,
    string? Description,
    EventLocationDto? Location,
    string? Color,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsAllDay,
    RecurrenceDto? Recurrence,
    IReadOnlyList<string> Guests,
    IReadOnlyList<int> ReminderMinutesBeforeStart,
    bool NotifyOnCreation,
    bool NotifyBeforeStart);

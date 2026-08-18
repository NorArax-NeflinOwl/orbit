namespace Orbit.Contracts.Calendar;

/// <param name="Guests">User ids of the invited guests - see CalendarEventDetails.Guests.</param>
public sealed record CalendarEventDetailsDto(
    string Title,
    string? Description,
    EventLocationDto? Location,
    string? Color,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsAllDay,
    RecurrenceDto? Recurrence,
    IReadOnlyList<Guid> Guests,
    IReadOnlyList<int> ReminderMinutesBeforeStart,
    bool NotifyOnCreation,
    bool NotifyBeforeStart);

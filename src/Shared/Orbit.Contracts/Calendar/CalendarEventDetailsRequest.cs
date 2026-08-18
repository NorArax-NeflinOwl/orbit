namespace Orbit.Contracts.Calendar;

/// <param name="Guests">User ids of the invited guests - see CalendarEventDetails.Guests.</param>
public sealed record CalendarEventDetailsRequest(
    string Title,
    string? Description,
    EventLocationRequest? Location,
    string? Color,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsAllDay,
    RecurrenceRequest? Recurrence,
    IReadOnlyList<Guid> Guests,
    IReadOnlyList<int> ReminderMinutesBeforeStart,
    bool NotifyOnCreation,
    bool NotifyBeforeStart);

namespace Orbit.Contracts.Calendar;

/// <param name="Guests">User ids of the invited guests - see CalendarEventDetails.Guests.</param>
/// <param name="CreationNotificationChannel">One of "None", "Email", "Push", "Both".</param>
/// <param name="ReminderNotificationChannel">One of "None", "Email", "Push", "Both".</param>
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
    string CreationNotificationChannel,
    string ReminderNotificationChannel);

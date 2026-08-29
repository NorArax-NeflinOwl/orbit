namespace Orbit.Contracts.Calendar;

/// <param name="Guests">User ids of the invited guests - see CalendarEventDetails.Guests.</param>
/// <param name="CreationNotificationChannel">One of "None", "Email", "Push", "Both".</param>
/// <param name="ReminderNotificationChannel">One of "None", "Email", "Push", "Both".</param>
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
    string CreationNotificationChannel,
    string ReminderNotificationChannel,
    /// <summary>ItemPriority by name - "Low", "Normal" or "High".</summary>
    string Priority = "Normal");

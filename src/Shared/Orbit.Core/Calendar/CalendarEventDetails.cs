namespace Orbit.Core.Calendar;

/// <summary>
/// Everything about a calendar event other than its identity and bookkeeping timestamps - grouped
/// together since CalendarEvent.Create and CalendarEvent.Update both take and replace this whole set at
/// once.
/// </summary>
/// <param name="Guests">
/// User ids of the invited guests, not their e-mail addresses or display names - both are resolved live
/// from the user's current profile when displayed (see GetCalendarEventByIdQueryHandler's callers), the
/// same way ContactSummary resolves a contact's profile rather than caching it.
/// </param>
public sealed record CalendarEventDetails(
    string Title,
    string? Description,
    EventLocation? Location,
    string? Color,
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    bool IsAllDay,
    EventRecurrence? Recurrence,
    IReadOnlyList<Guid> Guests,
    IReadOnlyList<int> ReminderMinutesBeforeStart,
    bool NotifyOnCreation,
    bool NotifyBeforeStart);

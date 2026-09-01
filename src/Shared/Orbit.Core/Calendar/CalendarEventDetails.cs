using Orbit.Core.Abstractions;
using Orbit.Core.Notifications;

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
/// <param name="ReminderNotificationChannel">
/// Which channel(s), if any, get the "event is approaching" notification sent as each entry in
/// <paramref name="ReminderMinutesBeforeStart"/> comes due - see
/// Reminders.EventReminderEmailContent/Reminders.EventReminderPushContent. Kept separate from the
/// configured lead times themselves, so an owner can keep them without clearing them while silencing
/// these notifications.
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
    NotificationChannel ReminderNotificationChannel,
    /// <summary>How much this event matters - see ItemPriority. Defaulted, so every existing caller reads as Normal.</summary>
    ItemPriority Priority = ItemPriority.Normal,
    /// <summary>
    /// Whether to say something when the event actually begins, as well as beforehand. Kept as its own
    /// flag rather than as a zero in <paramref name="ReminderMinutesBeforeStart"/>: it is a different
    /// question - "tell me it has started" against "tell me it is coming" - and a reminders table with
    /// a "0 minutes before" row in it reads as a mistake.
    /// </summary>
    bool NotifyAtStart = false)
{
    /// <summary>
    /// The lead times the scheduler actually works from. A notification at the start is a reminder zero
    /// minutes before it, so it is folded in here rather than given a path of its own - one due-time
    /// rule, one already-sent record, one place to be wrong.
    /// </summary>
    public IReadOnlyList<int> ReminderLeadTimesMinutes
        => NotifyAtStart && !ReminderMinutesBeforeStart.Contains(0)
            ? [.. ReminderMinutesBeforeStart, 0]
            : ReminderMinutesBeforeStart;
}

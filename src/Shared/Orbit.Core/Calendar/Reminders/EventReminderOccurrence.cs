namespace Orbit.Core.Calendar.Reminders;

/// <summary>
/// One reminder as it is told to somebody: the occurrence it is about (a repeating event's details
/// already moved onto the turn that is due), which event that is, and how long before its start it
/// was asked for. What EventReminderPushContent and EventReminderEmailContent gather when several fall
/// due for one reader in the same poll.
/// </summary>
public sealed record EventReminderOccurrence(CalendarEventDetails Details, Guid CalendarEventId, int MinutesBeforeStart);

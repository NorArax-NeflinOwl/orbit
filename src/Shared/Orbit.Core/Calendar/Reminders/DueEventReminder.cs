namespace Orbit.Core.Calendar.Reminders;

/// <summary>A single event reminder that has come due and hasn't been sent yet.</summary>
/// <param name="OccurrenceStartUtc">
/// The specific occurrence's start time this reminder is for - CalendarEvent.Details.StartUtc itself for a
/// non-recurring event, or one of its generated future occurrences (see CalendarEventOccurrenceGenerator)
/// for a recurring one.
/// </param>
public sealed record DueEventReminder(CalendarEvent CalendarEvent, int MinutesBeforeStart, DateTimeOffset OccurrenceStartUtc);

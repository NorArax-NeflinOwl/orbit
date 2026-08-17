namespace Orbit.Core.Calendar.Reminders;

/// <summary>A single event reminder that has come due and hasn't been sent yet.</summary>
public sealed record DueEventReminder(CalendarEvent CalendarEvent, int MinutesBeforeStart);

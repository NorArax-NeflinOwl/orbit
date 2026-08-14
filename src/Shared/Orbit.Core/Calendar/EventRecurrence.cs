namespace Orbit.Core.Calendar;

/// <summary>
/// Describes how a calendar event repeats: every IntervalCount units of Frequency, optionally stopping
/// after UntilUtc. An event without one is a single, non-repeating occurrence. Recurring events are
/// stored as one CalendarEvent carrying this rule; individual occurrences are not expanded server-side
/// yet (see the "Calendar" section in README.md).
/// </summary>
public sealed record EventRecurrence(RecurrenceFrequency Frequency, int IntervalCount, DateTimeOffset? UntilUtc);

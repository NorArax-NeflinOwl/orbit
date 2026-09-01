namespace Orbit.Core.Calendar;

/// <summary>
/// Describes how a calendar event repeats: every IntervalCount units of Frequency, stopping when either
/// limit is reached - after UntilUtc, or after OccurrenceCount occurrences, whichever comes first. An
/// event without one is a single, non-repeating occurrence. Recurring events are stored as one
/// CalendarEvent carrying this rule; individual occurrences are not expanded server-side yet (see the
/// "Calendar" section in README.md).
/// </summary>
/// <param name="OccurrenceCount">
/// How many occurrences there are in total, counting the first. Null means "no limit of this kind" -
/// the rule then runs until UntilUtc, or forever if that is null too. The two limits are independent
/// because people think in both: "every Monday until the end of term" and "four more sessions".
/// </param>
public sealed record EventRecurrence(
    RecurrenceFrequency Frequency, int IntervalCount, DateTimeOffset? UntilUtc, int? OccurrenceCount = null);

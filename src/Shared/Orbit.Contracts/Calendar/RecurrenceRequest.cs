namespace Orbit.Contracts.Calendar;

/// <summary>
/// Frequency is one of "Daily", "Weekly", "Monthly", "Yearly" (matches
/// Orbit.Core.Calendar.RecurrenceFrequency).
/// </summary>
/// <param name="OccurrenceCount">
/// How many occurrences in total, counting the first; null for no limit of this kind - see
/// Orbit.Core.Calendar.EventRecurrence.
/// </param>
public sealed record RecurrenceRequest(
    string Frequency, int IntervalCount, DateTimeOffset? UntilUtc, int? OccurrenceCount = null);

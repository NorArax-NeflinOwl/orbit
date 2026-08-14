namespace Orbit.Contracts.Calendar;

/// <summary>Frequency is one of "Daily", "Weekly", "Monthly" (matches Orbit.Core.Calendar.RecurrenceFrequency).</summary>
public sealed record RecurrenceRequest(string Frequency, int IntervalCount, DateTimeOffset? UntilUtc);

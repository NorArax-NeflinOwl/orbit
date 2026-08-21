namespace Orbit.Core.Calendar;

/// <summary>
/// Expands a recurring event's rule into the individual occurrence start times that fall within a given
/// window - used by EventReminderScheduler so every upcoming occurrence of a recurring event gets its own
/// reminder, not just the single StartUtc the event itself carries. A parallel implementation,
/// CalendarEventOccurrenceExpander in Orbit.Web, does the same job client-side for the calendar
/// visualization, operating on the wire DTOs instead of this domain model - the two aren't shared because
/// Orbit.Web never references Orbit.Core.
/// </summary>
public static class CalendarEventOccurrenceGenerator
{
    /// <summary>Safety net against a pathological interval/window combination iterating for a very long time.</summary>
    private const int MaxOccurrencesPerExpansion = 5000;

    /// <summary>
    /// Yields every occurrence start before windowEndExclusive, honoring recurrence.UntilUtc as a
    /// calendar-date cutoff (not a raw instant - see the comment below). A few occurrences before
    /// windowStart may also come back (see FastForwardToWindow) - callers are expected to filter further
    /// themselves, as EventReminderScheduler already does with its own due-time check.
    /// </summary>
    public static IEnumerable<DateTimeOffset> GenerateOccurrenceStarts(
        DateTimeOffset firstOccurrenceStart, EventRecurrence recurrence, DateTimeOffset windowStart, DateTimeOffset windowEndExclusive)
    {
        var occurrenceStart = FastForwardToWindow(firstOccurrenceStart, recurrence, windowStart);
        for (var iteration = 0; iteration < MaxOccurrencesPerExpansion && occurrenceStart < windowEndExclusive; iteration++)
        {
            // Compared as calendar dates (in each timestamp's own local offset), not raw instants: UntilUtc
            // is a date the user picked to stop repeating on, so an occurrence later that same day must
            // still count as within range.
            if (recurrence.UntilUtc is { } until && occurrenceStart.LocalDateTime.Date > until.LocalDateTime.Date)
            {
                yield break;
            }

            yield return occurrenceStart;
            occurrenceStart = StepForward(occurrenceStart, recurrence);
        }
    }

    /// <summary>
    /// Skips ahead to the last occurrence at or before windowStart (never earlier than
    /// firstOccurrenceStart itself), so a long-running Daily/Weekly recurrence doesn't have to be walked
    /// one step at a time just to reach a window far in its future - Monthly steps are already sparse
    /// enough that this isn't needed there.
    /// </summary>
    private static DateTimeOffset FastForwardToWindow(DateTimeOffset firstOccurrenceStart, EventRecurrence recurrence, DateTimeOffset windowStart)
    {
        if (firstOccurrenceStart >= windowStart || recurrence.Frequency == RecurrenceFrequency.Monthly)
        {
            return firstOccurrenceStart;
        }

        var stepDays = Math.Max(recurrence.IntervalCount, 1) * (recurrence.Frequency == RecurrenceFrequency.Weekly ? 7 : 1);
        var stepsToSkip = Math.Max(0, (long)((windowStart - firstOccurrenceStart).TotalDays / stepDays));
        return firstOccurrenceStart.AddDays(stepsToSkip * stepDays);
    }

    /// <summary>Interval count is clamped to at least 1 so a corrupt/zero value can never stall the occurrence generator in place.</summary>
    private static DateTimeOffset StepForward(DateTimeOffset occurrenceStart, EventRecurrence recurrence)
    {
        var intervalCount = Math.Max(recurrence.IntervalCount, 1);
        return recurrence.Frequency switch
        {
            RecurrenceFrequency.Daily => occurrenceStart.AddDays(intervalCount),
            RecurrenceFrequency.Weekly => occurrenceStart.AddDays(intervalCount * 7),
            RecurrenceFrequency.Monthly => occurrenceStart.AddMonths(intervalCount),
            _ => occurrenceStart.AddDays(intervalCount)
        };
    }
}

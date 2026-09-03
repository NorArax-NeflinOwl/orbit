using Orbit.Contracts.Calendar;

namespace Orbit.Web.Services;

/// <summary>
/// Expands a recurring calendar event's rule into synthetic per-occurrence copies for display, since the
/// API returns each recurring event as the single record it's stored as (see RecurrenceDto) rather than
/// one row per occurrence - see CalendarGridBuilder, the only caller. A parallel implementation,
/// CalendarEventOccurrenceGenerator in Orbit.Core.Calendar, does the same job server-side for reminder
/// scheduling; the two aren't shared because Orbit.Web only references Orbit.Contracts (the wire DTOs),
/// never the Orbit.Core domain model.
/// </summary>
public static class CalendarEventOccurrenceExpander
{
    /// <summary>Safety net against a pathological interval/window combination iterating for a very long time.</summary>
    private const int MaxOccurrencesPerExpansion = 2000;

    /// <summary>
    /// Returns calendarEvent unchanged if it doesn't recur. Otherwise yields one copy per occurrence whose
    /// start falls before windowEndExclusive, each with Details.StartUtc/EndUtc shifted to that occurrence
    /// while keeping the original duration. A few occurrences before windowStart may also come back -
    /// callers that care about an exact date/range (as CalendarGridBuilder does, per day) are expected to
    /// filter further themselves.
    /// </summary>
    public static IEnumerable<CalendarEventDto> ExpandOccurrences(
        CalendarEventDto calendarEvent, DateTimeOffset windowStart, DateTimeOffset windowEndExclusive)
    {
        var details = calendarEvent.Details;
        if (details.Recurrence is not { } recurrence)
        {
            yield return calendarEvent;
            yield break;
        }

        var duration = details.EndUtc - details.StartUtc;
        foreach (var occurrenceStart in GenerateOccurrenceStarts(details.StartUtc, recurrence, windowStart, windowEndExclusive))
        {
            yield return calendarEvent with { Details = details with { StartUtc = occurrenceStart, EndUtc = occurrenceStart + duration } };
        }
    }

    private static IEnumerable<DateTimeOffset> GenerateOccurrenceStarts(
        DateTimeOffset firstOccurrenceStart, RecurrenceDto recurrence, DateTimeOffset windowStart, DateTimeOffset windowEndExclusive)
    {
        var occurrenceStart = FastForwardToWindow(firstOccurrenceStart, recurrence, windowStart);
        for (var iteration = 0; iteration < MaxOccurrencesPerExpansion && occurrenceStart < windowEndExclusive; iteration++)
        {
            // Compared as calendar dates (in each timestamp's own local offset), not raw instants: UntilUtc
            // is a date the user picked to stop repeating on (see CalendarEventFormModel.ToOptionalDateTimeOffset),
            // so an occurrence later that same day must still count as within range.
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
    private static DateTimeOffset FastForwardToWindow(DateTimeOffset firstOccurrenceStart, RecurrenceDto recurrence, DateTimeOffset windowStart)
    {
        if (firstOccurrenceStart >= windowStart || recurrence.Frequency is "Monthly" or "Yearly")
        {
            return firstOccurrenceStart;
        }

        var stepDays = Math.Max(recurrence.IntervalCount, 1) * (recurrence.Frequency == "Weekly" ? 7 : 1);
        var stepsToSkip = Math.Max(0, (long)((windowStart - firstOccurrenceStart).TotalDays / stepDays));
        return firstOccurrenceStart.AddDays(stepsToSkip * stepDays);
    }

    /// <summary>Interval count is clamped to at least 1 so a corrupt/zero value can never stall the occurrence generator in place.</summary>
    private static DateTimeOffset StepForward(DateTimeOffset occurrenceStart, RecurrenceDto recurrence)
    {
        var intervalCount = Math.Max(recurrence.IntervalCount, 1);
        return recurrence.Frequency switch
        {
            "Daily" => occurrenceStart.AddDays(intervalCount),
            "Weekly" => occurrenceStart.AddDays(intervalCount * 7),
            "Monthly" => occurrenceStart.AddMonths(intervalCount),
            "Yearly" => occurrenceStart.AddYears(intervalCount),
            _ => occurrenceStart.AddDays(intervalCount)
        };
    }
}

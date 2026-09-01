using Orbit.Core.Calendar;
using Xunit;

namespace Orbit.Api.Tests.Calendar;

/// <summary>
/// The two ways a repeating event can be told to stop - on a date, or after so many times - and the
/// fourth way it can repeat. A rule with a count is not a rule with an end date: "four more sessions"
/// is a thing people say, and working out which Thursday that lands on is the app's job, not theirs.
/// </summary>
public sealed class RepeatLimitsTests
{
    private static readonly DateTimeOffset Start = new(2026, 1, 5, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_daily_rule_stops_after_the_number_of_times_it_was_given()
    {
        var recurrence = new EventRecurrence(RecurrenceFrequency.Daily, IntervalCount: 1, UntilUtc: null, OccurrenceCount: 3);

        var occurrences = Expand(recurrence, Start, Start.AddDays(30));

        // Three in total, counting the first: the 5th, 6th and 7th.
        Assert.Equal([Start, Start.AddDays(1), Start.AddDays(2)], occurrences);
    }

    [Fact]
    public void A_count_of_one_means_it_happens_once()
    {
        var recurrence = new EventRecurrence(RecurrenceFrequency.Weekly, IntervalCount: 1, UntilUtc: null, OccurrenceCount: 1);

        Assert.Equal([Start], Expand(recurrence, Start, Start.AddDays(60)));
    }

    [Fact]
    public void Without_a_count_it_keeps_going_to_the_end_of_the_window()
    {
        var recurrence = new EventRecurrence(RecurrenceFrequency.Daily, IntervalCount: 1, UntilUtc: null);

        Assert.Equal(10, Expand(recurrence, Start, Start.AddDays(10)).Count);
    }

    /// <summary>
    /// The count is a property of the series, not of whatever stretch of it somebody happens to be
    /// looking at - a window opening after the series has run out must show nothing, not start again.
    /// </summary>
    [Fact]
    public void A_window_that_opens_after_the_last_one_shows_nothing()
    {
        var recurrence = new EventRecurrence(RecurrenceFrequency.Daily, IntervalCount: 1, UntilUtc: null, OccurrenceCount: 3);

        Assert.Empty(Expand(recurrence, Start.AddDays(20), Start.AddDays(30)));
    }

    [Fact]
    public void The_earlier_of_the_two_limits_is_the_one_that_stops_it()
    {
        // Ten times, but only five days to do them in.
        var recurrence = new EventRecurrence(
            RecurrenceFrequency.Daily, IntervalCount: 1, UntilUtc: Start.AddDays(4), OccurrenceCount: 10);

        Assert.Equal(5, Expand(recurrence, Start, Start.AddDays(30)).Count);
    }

    [Fact]
    public void A_yearly_rule_lands_on_the_same_date_each_year()
    {
        var recurrence = new EventRecurrence(RecurrenceFrequency.Yearly, IntervalCount: 1, UntilUtc: null);

        var occurrences = Expand(recurrence, Start, Start.AddYears(3));

        Assert.Equal([Start, Start.AddYears(1), Start.AddYears(2)], occurrences);
    }

    private static IReadOnlyList<DateTimeOffset> Expand(
        EventRecurrence recurrence, DateTimeOffset windowStart, DateTimeOffset windowEndExclusive)
        => [.. CalendarEventOccurrenceGenerator.GenerateOccurrenceStarts(Start, recurrence, windowStart, windowEndExclusive)];
}

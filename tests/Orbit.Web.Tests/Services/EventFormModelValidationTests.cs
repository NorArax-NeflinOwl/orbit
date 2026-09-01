using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// What an event's form can tell about itself while it is being filled in. Moving the start drags the
/// end along, so the impossible cases are the ones somebody reaches by editing the other end - and
/// finding out about those on Save is finding out too late.
/// </summary>
public sealed class EventFormModelValidationTests
{
    private static readonly DateOnly Day = new(2026, 9, 14);

    [Fact]
    public void An_end_before_the_start_is_reported()
    {
        var form = AnEvent(from: new TimeOnly(10, 0), to: new TimeOnly(9, 0));

        Assert.True(form.EndsBeforeItStarts);
    }

    [Fact]
    public void An_end_after_the_start_is_not()
    {
        Assert.False(AnEvent(from: new TimeOnly(10, 0), to: new TimeOnly(11, 0)).EndsBeforeItStarts);
    }

    /// <summary>An event that ends the moment it starts is a point in time, not a mistake.</summary>
    [Fact]
    public void An_end_exactly_at_the_start_is_not()
    {
        Assert.False(AnEvent(from: new TimeOnly(10, 0), to: new TimeOnly(10, 0)).EndsBeforeItStarts);
    }

    /// <summary>
    /// An all-day event carries no times, so the two days are the whole of it - and the times left in
    /// the boxes must not decide anything.
    /// </summary>
    [Fact]
    public void An_all_day_event_is_judged_by_its_days_alone()
    {
        var form = AnEvent(from: new TimeOnly(10, 0), to: new TimeOnly(9, 0));
        form.IsAllDay = true;

        Assert.False(form.EndsBeforeItStarts);
    }

    [Fact]
    public void A_half_filled_form_says_nothing_is_wrong_yet()
    {
        // Nothing to compare against: an end nobody has given is not an end before the start.
        var form = AnEvent(from: new TimeOnly(10, 0), to: new TimeOnly(9, 0));
        form.EndDate = null;

        Assert.False(form.EndsBeforeItStarts);
    }

    [Fact]
    public void A_repeat_told_to_stop_before_it_starts_is_reported()
    {
        var form = AnEvent(from: new TimeOnly(10, 0), to: new TimeOnly(11, 0));
        form.IsRecurring = true;
        form.RecurrenceUntil = Day.AddDays(-1);

        Assert.True(form.StopsRepeatingBeforeItStarts);
    }

    [Fact]
    public void A_repeat_that_outlives_its_first_occurrence_is_not()
    {
        var form = AnEvent(from: new TimeOnly(10, 0), to: new TimeOnly(11, 0));
        form.IsRecurring = true;
        form.RecurrenceUntil = Day.AddDays(30);

        Assert.False(form.StopsRepeatingBeforeItStarts);
    }

    /// <summary>A one-off has no rule to be wrong about, whatever is left in the box.</summary>
    [Fact]
    public void A_one_off_is_not_asked_about_its_repeat()
    {
        var form = AnEvent(from: new TimeOnly(10, 0), to: new TimeOnly(11, 0));
        form.RecurrenceUntil = Day.AddDays(-1);

        Assert.False(form.StopsRepeatingBeforeItStarts);
    }

    private static EventFormModel AnEvent(TimeOnly from, TimeOnly to)
        => new() { StartDate = Day, StartTime = from, EndDate = Day, EndTime = to };
}

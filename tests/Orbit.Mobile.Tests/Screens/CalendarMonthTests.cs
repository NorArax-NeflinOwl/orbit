using Orbit.Contracts.Calendar;
using Orbit.Mobile.Data;
using Orbit.Mobile.Screens.Calendar;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The month grid the phone did not have. What it has to get right is the shape: always six weeks so
/// the grid does not change height as somebody pages through it, always starting Monday, and the days
/// either side of the month present but marked as not belonging to it.
/// </summary>
public sealed class CalendarMonthTests
{
    private static readonly DateTime August = new(2026, 8, 15);

    [Fact]
    public void The_grid_is_always_six_weeks()
    {
        // February 2026 starts on a Sunday and has 28 days - the month most likely to come out short.
        foreach (var month in new[] { August, new DateTime(2026, 2, 10), new DateTime(2027, 1, 20) })
        {
            Assert.Equal(42, CalendarMonth.Build(month, null, August, []).Count);
        }
    }

    [Fact]
    public void It_starts_on_a_Monday()
    {
        var days = CalendarMonth.Build(August, null, August, []);

        Assert.Equal(DayOfWeek.Monday, days[0].Date.DayOfWeek);
        Assert.True(days[0].Date <= new DateTime(2026, 8, 1));
    }

    /// <summary>
    /// Shown but quiet. A grid with holes in it is harder to read than one with soft edges, and a day
    /// that is there but not marked as belonging would be a lie about which month it is.
    /// </summary>
    [Fact]
    public void The_days_either_side_are_present_and_marked_as_outside()
    {
        var days = CalendarMonth.Build(August, null, August, []);

        Assert.Contains(days, day => !day.IsInMonth);
        Assert.All(days.Where(day => day.IsInMonth), day => Assert.Equal(8, day.Date.Month));
    }

    [Fact]
    public void A_day_with_something_on_it_says_so()
    {
        var days = CalendarMonth.Build(August, null, August, [EventOn(new DateTime(2026, 8, 20, 9, 0, 0))]);

        var twentieth = days.Single(day => day.Date == new DateTime(2026, 8, 20));
        Assert.True(twentieth.HasEvents);
        Assert.Equal(1, twentieth.EventCount);
        Assert.All(days.Where(day => day.Date != new DateTime(2026, 8, 20)), day => Assert.False(day.HasEvents));
    }

    [Fact]
    public void Two_events_on_one_day_are_counted_together()
    {
        var days = CalendarMonth.Build(
            August, null, August,
            [EventOn(new DateTime(2026, 8, 20, 9, 0, 0)), EventOn(new DateTime(2026, 8, 20, 17, 0, 0))]);

        Assert.Equal(2, days.Single(day => day.Date == new DateTime(2026, 8, 20)).EventCount);
    }

    [Fact]
    public void Today_and_the_chosen_day_are_each_marked()
    {
        var days = CalendarMonth.Build(August, new DateTime(2026, 8, 20), August, []);

        Assert.True(days.Single(day => day.Date == August.Date).IsToday);
        Assert.True(days.Single(day => day.Date == new DateTime(2026, 8, 20)).IsSelected);
        Assert.False(days.Single(day => day.Date == new DateTime(2026, 8, 20)).IsToday);
    }

    private static LocalCalendarEvent EventOn(DateTime localStart)
    {
        var start = new DateTimeOffset(localStart, TimeZoneInfo.Local.GetUtcOffset(localStart));

        return new LocalCalendarEvent
        {
            LocalId = Guid.NewGuid(),
            Details = new CalendarEventDetailsDto(
                "Meeting", null, null, null, start.ToUniversalTime(), start.AddHours(1).ToUniversalTime(),
                false, null, [], [], "None", "None")
        };
    }
}

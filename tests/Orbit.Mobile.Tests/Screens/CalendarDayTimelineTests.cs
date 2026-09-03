using Orbit.Contracts.Calendar;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Calendar;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// A day laid out by the hour. The phone answered "what is on today" with a list, which loses the three
/// things the question is usually about: how long each thing takes, what clashes with what, and where
/// the gaps are. Orbit.Web's day view has drawn all three all along.
///
/// The placement is what is worth pinning: everything else on this screen is a rectangle.
/// </summary>
public sealed class CalendarDayTimelineTests
{
    private static readonly DateTime Day = new(2026, 8, 29);

    [Fact]
    public void An_event_sits_at_the_hour_it_starts_and_is_as_tall_as_it_lasts()
    {
        var block = Assert.Single(Build(At(9, 0, 10, 30, "Standup")));

        Assert.Equal(9 * 60, block.StartMinute);
        Assert.Equal(10 * 60 + 30, block.EndMinute);
        Assert.Equal(90, block.Minutes);
    }

    /// <summary>
    /// A minute-long event would be a line too thin to read or tap, so it is drawn at a height a finger
    /// can find. It still starts where it starts.
    /// </summary>
    [Fact]
    public void Something_too_short_to_draw_is_still_given_room()
    {
        var block = Assert.Single(Build(At(9, 0, 9, 5, "Call back")));

        Assert.Equal(DayBlock.MinimumMinutes, block.Minutes);
        Assert.Equal(9 * 60, block.StartMinute);
    }

    /// <summary>Two at once is the whole reason a day is drawn rather than listed.</summary>
    [Fact]
    public void Two_things_at_the_same_time_are_drawn_side_by_side()
    {
        var blocks = Build(At(9, 0, 10, 0, "Standup"), At(9, 30, 10, 30, "Dentist"));

        Assert.All(blocks, block => Assert.Equal(2, block.ColumnCount));
        Assert.Equal([0, 1], blocks.OrderBy(block => block.StartMinute).Select(block => block.Column));
    }

    /// <summary>
    /// A lane is reused as soon as whatever held it has finished, so a day of back-to-back meetings
    /// stays one column wide rather than growing a lane per meeting.
    /// </summary>
    [Fact]
    public void Things_that_do_not_overlap_share_one_lane()
    {
        var blocks = Build(At(9, 0, 10, 0, "Standup"), At(11, 0, 12, 0, "Lunch"), At(13, 0, 14, 0, "Review"));

        Assert.All(blocks, block => Assert.Equal(1, block.ColumnCount));
        Assert.All(blocks, block => Assert.Equal(0, block.Column));
    }

    /// <summary>
    /// A busy hour widens only the run it is in. Two lanes across the whole day because of one clash at
    /// nine would make every other hour half as wide for nothing.
    /// </summary>
    [Fact]
    public void A_clash_widens_only_what_it_clashes_with()
    {
        var blocks = Build(At(9, 0, 10, 0, "Standup"), At(9, 30, 10, 30, "Dentist"), At(15, 0, 16, 0, "Review"));

        Assert.Equal(1, blocks.Single(block => block.Title == "Review").ColumnCount);
        Assert.Equal(2, blocks.Single(block => block.Title == "Standup").ColumnCount);
    }

    /// <summary>
    /// Something that began yesterday evening is on this morning too, drawn to the edge of the day
    /// rather than off it - a block starting at minus two hours is a block nobody can see.
    /// </summary>
    [Fact]
    public void Something_running_in_from_yesterday_starts_at_the_top_of_the_day()
    {
        var overnight = An(
            "Night shift",
            Day.AddDays(-1).AddHours(22),
            Day.AddHours(6));

        var block = Assert.Single(CalendarDayTimeline.Build(Day, [overnight], English()));

        Assert.Equal(0, block.StartMinute);
        Assert.Equal(6 * 60, block.EndMinute);
    }

    /// <summary>And out the other side: what runs past midnight stops at the bottom of the day.</summary>
    [Fact]
    public void Something_running_out_past_midnight_stops_at_the_bottom()
    {
        var overnight = An("Night shift", Day.AddHours(22), Day.AddDays(1).AddHours(6));

        var block = Assert.Single(CalendarDayTimeline.Build(Day, [overnight], English()));

        Assert.Equal(22 * 60, block.StartMinute);
        Assert.Equal(CalendarDayTimeline.MinutesInADay, block.EndMinute);
    }

    [Fact]
    public void Another_days_events_are_not_on_this_one()
    {
        var tomorrow = An("Tomorrow", Day.AddDays(1).AddHours(9), Day.AddDays(1).AddHours(10));

        Assert.Empty(CalendarDayTimeline.Build(Day, [tomorrow], English()));
    }

    /// <summary>
    /// An all-day event belongs to the whole day rather than an hour of it, so it is not placed on the
    /// clock - drawing it as a block from midnight to midnight would bury everything behind it.
    /// </summary>
    [Fact]
    public void An_all_day_event_is_kept_off_the_clock()
    {
        var holiday = An("Bank holiday", Day, Day.AddDays(1));
        holiday.Details = holiday.Details with { IsAllDay = true };

        Assert.Empty(CalendarDayTimeline.Build(Day, [holiday], English()));
        Assert.Equal("Bank holiday", Assert.Single(CalendarDayTimeline.AllDayOn(Day, [holiday], English())).Title);
    }

    /// <summary>
    /// All twenty-four hours would be mostly empty: a day with one meeting at nine would open on
    /// midnight and ask the reader to scroll past eight hours of nothing to find it.
    /// </summary>
    [Fact]
    public void Only_the_hours_the_day_uses_are_worth_drawing()
    {
        var hours = CalendarDayTimeline.HoursWorthDrawing(Build(At(9, 0, 10, 30, "Standup")));

        Assert.Equal(9, hours.FirstHour);
        Assert.Equal(10, hours.LastHour);
    }

    /// <summary>An hour with something in it is drawn whole, so an event ending at 10:30 keeps its hour.</summary>
    [Fact]
    public void The_last_hour_is_drawn_whole()
    {
        var hours = CalendarDayTimeline.HoursWorthDrawing(Build(At(9, 0, 10, 1, "Standup")));

        Assert.Equal(10, hours.LastHour);
    }

    /// <summary>Something running to midnight belongs to the last hour there is, not the first of a day nobody asked for.</summary>
    [Fact]
    public void An_evening_that_runs_to_midnight_stops_at_the_last_hour()
    {
        var hours = CalendarDayTimeline.HoursWorthDrawing(Build(At(22, 0, 23, 59, "Night shift")));

        Assert.Equal(22, hours.FirstHour);
        Assert.Equal(23, hours.LastHour);
    }

    private static IReadOnlyList<DayBlock> Build(params LocalCalendarEvent[] events)
        => CalendarDayTimeline.Build(Day, events, English());

    private static LocalCalendarEvent At(int fromHour, int fromMinute, int toHour, int toMinute, string title)
        => An(title, Day.AddHours(fromHour).AddMinutes(fromMinute), Day.AddHours(toHour).AddMinutes(toMinute));

    private static LocalCalendarEvent An(string title, DateTime localStart, DateTime localEnd)
        => new()
        {
            LocalId = Guid.NewGuid(),
            ServerId = Guid.NewGuid(),
            Details = new CalendarEventDetailsDto(
                title, null, null, null, ToUtc(localStart), ToUtc(localEnd), false, null, [], [], ReminderNotificationChannel: "None")
        };

    private static DateTimeOffset ToUtc(DateTime local)
        => new DateTimeOffset(local, TimeZoneInfo.Local.GetUtcOffset(local)).ToUniversalTime();

    private static Translations English() => new(new InMemoryLanguageStore());
}

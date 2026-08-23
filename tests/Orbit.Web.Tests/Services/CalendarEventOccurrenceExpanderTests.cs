using Orbit.Contracts.Calendar;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

public sealed class CalendarEventOccurrenceExpanderTests
{
    [Fact]
    public void A_non_recurring_event_passes_through_unchanged()
    {
        var calendarEvent = CreateEvent(new DateTime(2026, 8, 21, 9, 0, 0), new DateTime(2026, 8, 21, 10, 0, 0), recurrence: null);

        var occurrences = CalendarEventOccurrenceExpander.ExpandOccurrences(
            calendarEvent, ToLocal(new DateTime(2026, 1, 1)), ToLocal(new DateTime(2027, 1, 1))).ToList();

        Assert.Same(calendarEvent, Assert.Single(occurrences));
    }

    [Fact]
    public void A_daily_recurrence_yields_one_occurrence_per_day_within_the_window()
    {
        var calendarEvent = CreateEvent(
            new DateTime(2026, 8, 21, 9, 0, 0), new DateTime(2026, 8, 21, 9, 30, 0), new RecurrenceDto("Daily", 1, null));

        var occurrences = CalendarEventOccurrenceExpander.ExpandOccurrences(
            calendarEvent, ToLocal(new DateTime(2026, 8, 21)), ToLocal(new DateTime(2026, 8, 24))).ToList();

        var startDates = occurrences.Select(occurrence => occurrence.Details.StartUtc.LocalDateTime.Date).ToList();
        Assert.Equal(
            [new DateTime(2026, 8, 21), new DateTime(2026, 8, 22), new DateTime(2026, 8, 23)], startDates);
        // Every occurrence keeps the original 30-minute duration and time of day.
        Assert.All(occurrences, occurrence => Assert.Equal(TimeSpan.FromMinutes(30), occurrence.Details.EndUtc - occurrence.Details.StartUtc));
        Assert.All(occurrences, occurrence => Assert.Equal(new TimeOnly(9, 0), TimeOnly.FromDateTime(occurrence.Details.StartUtc.LocalDateTime)));
    }

    [Fact]
    public void A_weekly_recurrence_with_interval_2_steps_two_weeks_at_a_time()
    {
        var calendarEvent = CreateEvent(
            new DateTime(2026, 8, 1, 18, 0, 0), new DateTime(2026, 8, 1, 19, 0, 0), new RecurrenceDto("Weekly", 2, null));

        var occurrences = CalendarEventOccurrenceExpander.ExpandOccurrences(
            calendarEvent, ToLocal(new DateTime(2026, 8, 1)), ToLocal(new DateTime(2026, 9, 15))).ToList();

        var startDates = occurrences.Select(occurrence => occurrence.Details.StartUtc.LocalDateTime.Date).ToList();
        Assert.Equal([new DateTime(2026, 8, 1), new DateTime(2026, 8, 15), new DateTime(2026, 8, 29), new DateTime(2026, 9, 12)], startDates);
    }

    [Fact]
    public void A_monthly_recurrence_repeats_on_the_same_day_of_month()
    {
        var calendarEvent = CreateEvent(
            new DateTime(2026, 1, 15, 12, 0, 0), new DateTime(2026, 1, 15, 13, 0, 0), new RecurrenceDto("Monthly", 1, null));

        var occurrences = CalendarEventOccurrenceExpander.ExpandOccurrences(
            calendarEvent, ToLocal(new DateTime(2026, 1, 1)), ToLocal(new DateTime(2026, 5, 1))).ToList();

        var startDates = occurrences.Select(occurrence => occurrence.Details.StartUtc.LocalDateTime.Date).ToList();
        Assert.Equal(
            [new DateTime(2026, 1, 15), new DateTime(2026, 2, 15), new DateTime(2026, 3, 15), new DateTime(2026, 4, 15)], startDates);
    }

    [Fact]
    public void UntilUtc_stops_generation_after_the_last_allowed_occurrence()
    {
        var calendarEvent = CreateEvent(
            new DateTime(2026, 8, 1, 9, 0, 0), new DateTime(2026, 8, 1, 10, 0, 0),
            new RecurrenceDto("Daily", 1, ToLocal(new DateTime(2026, 8, 3))));

        var occurrences = CalendarEventOccurrenceExpander.ExpandOccurrences(
            calendarEvent, ToLocal(new DateTime(2026, 8, 1)), ToLocal(new DateTime(2026, 8, 31))).ToList();

        var startDates = occurrences.Select(occurrence => occurrence.Details.StartUtc.LocalDateTime.Date).ToList();
        Assert.Equal([new DateTime(2026, 8, 1), new DateTime(2026, 8, 2), new DateTime(2026, 8, 3)], startDates);
    }

    [Fact]
    public void A_daily_recurrence_that_started_years_before_the_window_still_reaches_it()
    {
        // Exercises the fast-forward path (GenerateOccurrenceStarts/FastForwardToWindow): without it,
        // walking one day at a time from 2020 would need thousands of iterations to reach this window.
        // The fast-forward only promises landing at-or-before windowStart, so one occurrence from just
        // before it may also come back - see ExpandOccurrences' documented contract.
        var calendarEvent = CreateEvent(
            new DateTime(2020, 1, 1, 8, 0, 0), new DateTime(2020, 1, 1, 8, 15, 0), new RecurrenceDto("Daily", 1, null));

        var occurrences = CalendarEventOccurrenceExpander.ExpandOccurrences(
            calendarEvent, ToLocal(new DateTime(2026, 8, 21)), ToLocal(new DateTime(2026, 8, 22))).ToList();

        Assert.Contains(occurrences, occurrence => occurrence.Details.StartUtc.LocalDateTime.Date == new DateTime(2026, 8, 21));
        Assert.All(occurrences, occurrence => Assert.True(occurrence.Details.StartUtc.LocalDateTime.Date <= new DateTime(2026, 8, 21)));
    }

    [Fact]
    public void An_interval_count_of_zero_or_less_is_treated_as_1_instead_of_stalling()
    {
        var calendarEvent = CreateEvent(
            new DateTime(2026, 8, 1, 9, 0, 0), new DateTime(2026, 8, 1, 10, 0, 0), new RecurrenceDto("Daily", 0, null));

        var occurrences = CalendarEventOccurrenceExpander.ExpandOccurrences(
            calendarEvent, ToLocal(new DateTime(2026, 8, 1)), ToLocal(new DateTime(2026, 8, 4))).ToList();

        var startDates = occurrences.Select(occurrence => occurrence.Details.StartUtc.LocalDateTime.Date).Distinct().ToList();
        Assert.Equal([new DateTime(2026, 8, 1), new DateTime(2026, 8, 2), new DateTime(2026, 8, 3)], startDates);
    }

    private static CalendarEventDto CreateEvent(DateTime localStart, DateTime localEnd, RecurrenceDto? recurrence, string title = "Event")
        => new(
            Guid.NewGuid(),
            new CalendarEventDetailsDto(title, null, null, null, ToLocal(localStart), ToLocal(localEnd), false, recurrence, [], [], "None", "None"),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, IsShared: false, SharedByUserName: null, AccessLevel: "ReadOnly", OriginalOwnerUserId: null);

    private static DateTimeOffset ToLocal(DateTime localDateTime) => new(DateTime.SpecifyKind(localDateTime, DateTimeKind.Local));
}

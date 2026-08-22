using Orbit.Contracts.Calendar;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

public sealed class CalendarGridBuilderTests
{
    [Fact]
    public void Month_grid_is_made_of_complete_Monday_to_Sunday_weeks()
    {
        var weeks = CalendarGridBuilder.BuildMonthGrid(new DateOnly(2026, 8, 1), [], []);

        Assert.All(weeks, week => Assert.Equal(7, week.Days.Count));
        Assert.All(weeks, week => Assert.Equal(DayOfWeek.Monday, week.Days[0].Date.DayOfWeek));
        Assert.All(weeks, week => Assert.Equal(DayOfWeek.Sunday, week.Days[6].Date.DayOfWeek));
    }

    [Fact]
    public void Month_grid_marks_the_requested_month_days_and_dims_the_borrowed_leading_and_trailing_days()
    {
        var weeks = CalendarGridBuilder.BuildMonthGrid(new DateOnly(2026, 8, 1), [], []);
        var allDays = weeks.SelectMany(week => week.Days).ToList();

        var daysInAugust = allDays.Where(day => day.Date.Month == 8 && day.Date.Year == 2026).ToList();
        Assert.Equal(31, daysInAugust.Count);
        Assert.All(daysInAugust, day => Assert.True(day.IsInDisplayedMonth));

        var borrowedDays = allDays.Where(day => day.Date.Month != 8 || day.Date.Year != 2026).ToList();
        Assert.NotEmpty(borrowedDays);
        Assert.All(borrowedDays, day => Assert.False(day.IsInDisplayedMonth));
    }

    [Fact]
    public void An_event_is_placed_only_on_its_own_day_cell()
    {
        var calendarEvent = CreateTimedEvent(new DateTime(2026, 8, 21, 10, 0, 0), new DateTime(2026, 8, 21, 11, 0, 0));

        var weeks = CalendarGridBuilder.BuildMonthGrid(new DateOnly(2026, 8, 1), [calendarEvent], []);
        var allDays = weeks.SelectMany(week => week.Days).ToList();

        var dayWithEvent = allDays.Single(day => day.Events.Count > 0);
        Assert.Equal(new DateOnly(2026, 8, 21), dayWithEvent.Date);
        Assert.Same(calendarEvent, dayWithEvent.Events.Single());
    }

    [Fact]
    public void A_multi_day_all_day_event_appears_on_every_date_it_spans()
    {
        var calendarEvent = CreateAllDayEvent(new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 22));

        var weeks = CalendarGridBuilder.BuildMonthGrid(new DateOnly(2026, 8, 1), [calendarEvent], []);
        var datesWithEvent = weeks.SelectMany(week => week.Days).Where(day => day.Events.Count > 0).Select(day => day.Date).ToList();

        Assert.Equal([new DateOnly(2026, 8, 20), new DateOnly(2026, 8, 21), new DateOnly(2026, 8, 22)], datesWithEvent);
    }

    [Fact]
    public void A_due_task_is_placed_only_on_its_own_day_cell()
    {
        var dueTask = CreateDueTask(new DateTime(2026, 8, 21, 10, 0, 0));

        var weeks = CalendarGridBuilder.BuildMonthGrid(new DateOnly(2026, 8, 1), [], [dueTask]);
        var allDays = weeks.SelectMany(week => week.Days).ToList();

        var dayWithTask = allDays.Single(day => day.DueTasks.Count > 0);
        Assert.Equal(new DateOnly(2026, 8, 21), dayWithTask.Date);
        Assert.Same(dueTask, dayWithTask.DueTasks.Single());
    }

    [Fact]
    public void Year_grid_contains_all_12_months_in_order_each_matching_BuildMonthGrid()
    {
        var months = CalendarGridBuilder.BuildYearGrid(2026, [], []);

        Assert.Equal(Enumerable.Range(1, 12), months.Select(month => month.Month));
        foreach (var month in months)
        {
            var expectedDates = CalendarGridBuilder.BuildMonthGrid(new DateOnly(2026, month.Month, 1), [], [])
                .SelectMany(week => week.Days).Select(day => day.Date);
            var actualDates = month.Weeks.SelectMany(week => week.Days).Select(day => day.Date);
            Assert.Equal(expectedDates, actualDates);
        }
    }

    [Fact]
    public void Day_grid_separates_all_day_events_from_the_timed_timeline()
    {
        var allDayEvent = CreateAllDayEvent(new DateOnly(2026, 8, 21), new DateOnly(2026, 8, 21));
        var timedEvent = CreateTimedEvent(new DateTime(2026, 8, 21, 9, 0, 0), new DateTime(2026, 8, 21, 10, 0, 0));

        var grid = CalendarGridBuilder.BuildDayGrid(new DateOnly(2026, 8, 21), [allDayEvent, timedEvent], []);

        Assert.Same(allDayEvent, Assert.Single(grid.AllDayEvents));
        Assert.Same(timedEvent, Assert.Single(grid.TimedEvents).Event);
    }

    [Fact]
    public void Day_grid_includes_due_tasks_due_on_that_day_and_excludes_ones_due_on_other_days()
    {
        var dueTodayTask = CreateDueTask(new DateTime(2026, 8, 21, 18, 0, 0));
        var dueTomorrowTask = CreateDueTask(new DateTime(2026, 8, 22, 9, 0, 0));

        var grid = CalendarGridBuilder.BuildDayGrid(new DateOnly(2026, 8, 21), [], [dueTodayTask, dueTomorrowTask]);

        Assert.Same(dueTodayTask, Assert.Single(grid.DueTasks));
    }

    [Fact]
    public void Day_grid_expresses_a_timed_events_span_in_minutes_since_local_midnight()
    {
        var calendarEvent = CreateTimedEvent(new DateTime(2026, 8, 21, 9, 30, 0), new DateTime(2026, 8, 21, 11, 15, 0));

        var grid = CalendarGridBuilder.BuildDayGrid(new DateOnly(2026, 8, 21), [calendarEvent], []);

        var placedEvent = Assert.Single(grid.TimedEvents);
        Assert.Equal(9 * 60 + 30, placedEvent.StartMinute);
        Assert.Equal(11 * 60 + 15, placedEvent.EndMinute);
    }

    [Fact]
    public void Day_grid_clamps_an_event_spanning_midnight_to_the_requested_days_bounds()
    {
        var calendarEvent = CreateTimedEvent(new DateTime(2026, 8, 20, 22, 0, 0), new DateTime(2026, 8, 21, 2, 0, 0));

        var gridForFirstDay = CalendarGridBuilder.BuildDayGrid(new DateOnly(2026, 8, 20), [calendarEvent], []);
        var gridForSecondDay = CalendarGridBuilder.BuildDayGrid(new DateOnly(2026, 8, 21), [calendarEvent], []);

        var placedOnFirstDay = Assert.Single(gridForFirstDay.TimedEvents);
        Assert.Equal(22 * 60, placedOnFirstDay.StartMinute);
        Assert.Equal(CalendarGridBuilder.MinutesPerDay, placedOnFirstDay.EndMinute);

        var placedOnSecondDay = Assert.Single(gridForSecondDay.TimedEvents);
        Assert.Equal(0, placedOnSecondDay.StartMinute);
        Assert.Equal(2 * 60, placedOnSecondDay.EndMinute);
    }

    [Fact]
    public void Non_overlapping_events_each_get_the_full_width_column()
    {
        var morningEvent = CreateTimedEvent(new DateTime(2026, 8, 21, 9, 0, 0), new DateTime(2026, 8, 21, 10, 0, 0));
        var afternoonEvent = CreateTimedEvent(new DateTime(2026, 8, 21, 14, 0, 0), new DateTime(2026, 8, 21, 15, 0, 0));

        var grid = CalendarGridBuilder.BuildDayGrid(new DateOnly(2026, 8, 21), [morningEvent, afternoonEvent], []);

        Assert.All(grid.TimedEvents, placedEvent =>
        {
            Assert.Equal(0, placedEvent.ColumnIndex);
            Assert.Equal(1, placedEvent.ColumnCount);
        });
    }

    [Fact]
    public void Two_overlapping_events_are_placed_in_separate_columns_sharing_the_width()
    {
        var firstEvent = CreateTimedEvent(new DateTime(2026, 8, 21, 9, 0, 0), new DateTime(2026, 8, 21, 10, 0, 0));
        var overlappingEvent = CreateTimedEvent(new DateTime(2026, 8, 21, 9, 30, 0), new DateTime(2026, 8, 21, 10, 30, 0));

        var grid = CalendarGridBuilder.BuildDayGrid(new DateOnly(2026, 8, 21), [firstEvent, overlappingEvent], []);

        Assert.Equal(2, grid.TimedEvents.Count);
        Assert.All(grid.TimedEvents, placedEvent => Assert.Equal(2, placedEvent.ColumnCount));
        Assert.Equal([0, 1], grid.TimedEvents.Select(placedEvent => placedEvent.ColumnIndex).OrderBy(index => index));
    }

    [Fact]
    public void A_third_event_overlapping_only_the_first_of_two_already_placed_events_reuses_the_freed_column()
    {
        // 09:00-10:00 and 09:30-10:30 overlap and take columns 0 and 1; 10:00-11:00 only overlaps the
        // second one, so it should reuse column 0 rather than opening up an unnecessary third column.
        var firstEvent = CreateTimedEvent(new DateTime(2026, 8, 21, 9, 0, 0), new DateTime(2026, 8, 21, 10, 0, 0));
        var secondEvent = CreateTimedEvent(new DateTime(2026, 8, 21, 9, 30, 0), new DateTime(2026, 8, 21, 10, 30, 0));
        var thirdEvent = CreateTimedEvent(new DateTime(2026, 8, 21, 10, 0, 0), new DateTime(2026, 8, 21, 11, 0, 0));

        var grid = CalendarGridBuilder.BuildDayGrid(new DateOnly(2026, 8, 21), [firstEvent, secondEvent, thirdEvent], []);

        Assert.All(grid.TimedEvents, placedEvent => Assert.Equal(2, placedEvent.ColumnCount));
        var thirdPlaced = grid.TimedEvents.Single(placedEvent => placedEvent.Event == thirdEvent);
        Assert.Equal(0, thirdPlaced.ColumnIndex);
    }

    private static CalendarEventDto CreateTimedEvent(DateTime localStart, DateTime localEnd, string title = "Event")
        => new(
            Guid.NewGuid(),
            new CalendarEventDetailsDto(
                title, null, null, null, ToLocalOffset(localStart), ToLocalOffset(localEnd), IsAllDay: false, null, [], [], "None", "None"),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, IsShared: false, SharedByUserName: null);

    private static CalendarEventDto CreateAllDayEvent(DateOnly startDate, DateOnly endDate, string title = "All-day event")
        => new(
            Guid.NewGuid(),
            new CalendarEventDetailsDto(
                title, null, null, null,
                ToLocalOffset(startDate.ToDateTime(TimeOnly.MinValue)), ToLocalOffset(endDate.ToDateTime(TimeOnly.MinValue)),
                IsAllDay: true, null, [], [], "None", "None"),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, IsShared: false, SharedByUserName: null);

    private static DueTaskDto CreateDueTask(DateTime localDueDate, string description = "Task")
        => new(Guid.NewGuid(), Guid.NewGuid(), description, ToLocalOffset(localDueDate), IsCompleted: false);

    /// <summary>
    /// Attaches the test machine's own local UTC offset to localDateTime, mirroring how
    /// CalendarEventEditor's form model builds these timestamps (DateTimeKind.Local) - so
    /// Details.StartUtc.LocalDateTime round-trips back to exactly localDateTime regardless of which
    /// timezone the test happens to run in.
    /// </summary>
    private static DateTimeOffset ToLocalOffset(DateTime localDateTime)
        => new(DateTime.SpecifyKind(localDateTime, DateTimeKind.Local));
}

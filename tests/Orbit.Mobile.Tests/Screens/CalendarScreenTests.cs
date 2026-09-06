using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Calendar;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The calendar's view switch. Orbit.Web offers day, month and year; here the day is the month grid
/// with one of its days chosen, and this covers the rest: the year overview, what the arrows step by
/// while it is showing, and the list beneath following whichever is on screen.
/// </summary>
public sealed class CalendarScreenTests
{
    /// <summary>
    /// The appointments among what the screen lists. The screen holds one list of both kinds now - see
    /// CalendarListEntry - and these keep each test asking about the kind it is about.
    /// </summary>
    private static IEnumerable<CalendarEventRow> Events(CalendarViewModel screen)
        => screen.Listed.Where(entry => entry.Event is not null).Select(entry => entry.Event!);

    private static IEnumerable<CalendarDeadline> Deadlines(CalendarViewModel screen)
        => screen.Listed.Where(entry => entry.Deadline is not null).Select(entry => entry.Deadline!);

    [Fact]
    public async Task The_month_is_what_opens()
    {
        using var context = new ScreenContext();

        var screen = await context.OpenAsync();

        Assert.True(screen.IsShowingMonth);
        Assert.False(screen.IsShowingYear);
        Assert.Equal("August 2026", screen.PeriodLabel);
    }

    /// <summary>
    /// Which month opens comes from the clock the screen was given, not from the machine's.
    ///
    /// It did not, and the whole of this class was quietly resting on the two agreeing: the fixtures
    /// are pinned to August 2026 through a FakeTimeProvider, while the grid opened on DateTime.Today.
    /// That held for as long as the machine happened to be in that month and then failed everywhere at
    /// once, on the 1st of September - thirteen tests, none of them about the clock.
    /// </summary>
    [Fact]
    public async Task The_month_that_opens_is_the_clocks_and_not_the_machines()
    {
        using var context = new ScreenContext("2027-03-09T10:00:00Z");

        var screen = await context.OpenAsync();

        Assert.Equal("March 2027", screen.PeriodLabel);
    }

    [Fact]
    public async Task The_year_shows_twelve_months_and_says_which_year()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();

        await screen.ShowYearCommand.ExecuteAsync(null);

        Assert.Equal(12, screen.Months.Count);
        Assert.Equal("2026", screen.PeriodLabel);
        Assert.False(screen.IsShowingMonth);
    }

    /// <summary>
    /// The arrows step by whatever is on screen. Stepping a month while looking at a year would move
    /// the header without visibly changing anything in front of the reader.
    /// </summary>
    [Fact]
    public async Task The_arrows_step_by_whatever_is_showing()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();

        await screen.ShowLaterCommand.ExecuteAsync(null);
        Assert.Equal("September 2026", screen.PeriodLabel);

        await screen.ShowYearCommand.ExecuteAsync(null);
        await screen.ShowLaterCommand.ExecuteAsync(null);
        Assert.Equal("2027", screen.PeriodLabel);

        await screen.ShowEarlierCommand.ExecuteAsync(null);
        Assert.Equal("2026", screen.PeriodLabel);
    }

    [Fact]
    public async Task Choosing_a_month_in_the_year_opens_it()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        await screen.ShowYearCommand.ExecuteAsync(null);

        await screen.ChooseMonthCommand.ExecuteAsync(screen.Months[10]);

        Assert.True(screen.IsShowingMonth);
        Assert.Equal("November 2026", screen.PeriodLabel);
    }

    /// <summary>The list beneath the grid has always followed it, and the grid can now be a year.</summary>
    [Fact]
    public async Task The_list_beneath_widens_to_the_year_and_narrows_back()
    {
        using var context = new ScreenContext();
        await context.AddEventAsync("Dentist", new DateTime(2026, 8, 20, 9, 0, 0));
        await context.AddEventAsync("Concert", new DateTime(2026, 11, 2, 19, 0, 0));
        await context.AddEventAsync("Next year", new DateTime(2027, 3, 1, 9, 0, 0));
        var screen = await context.OpenAsync();

        Assert.Equal(["Dentist"], Events(screen).Select(row => row.Title));

        await screen.ShowYearCommand.ExecuteAsync(null);
        Assert.Equal(["Dentist", "Concert"], Events(screen).Select(row => row.Title));

        await screen.ShowMonthCommand.ExecuteAsync(null);
        Assert.Equal(["Dentist"], Events(screen).Select(row => row.Title));
    }

    /// <summary>
    /// A repeat is a rule, not rows: the API returns one record and the phone drew it once, at its
    /// start. A weekly standup appeared in the week it began and never again, which reads exactly like
    /// an event that had stopped. Orbit.Web's grid has expanded repeats all along.
    /// </summary>
    [Fact]
    public async Task A_repeating_event_is_on_every_day_it_falls_on()
    {
        using var context = new ScreenContext();
        await context.AddEventAsync(
            "Standup", new DateTime(2026, 8, 3, 9, 0, 0), Weekly());
        var screen = await context.OpenAsync();
        // Counting the ones already past, which the list leaves out by default - this is about the rule
        // being walked, not about what a reader is shown of it.
        screen.ShowsEverything = true;

        // Five Mondays in August 2026, counting the one it starts on.
        Assert.Equal(5, Events(screen).Count(row => row.Title == "Standup"));
        foreach (var monday in new[] { 3, 10, 17, 24, 31 })
        {
            Assert.True(screen.Days.Single(day => day.Date == new DateTime(2026, 8, monday)).HasEvents);
        }
    }

    /// <summary>
    /// What a calendar is read for is what is coming, so the list leaves out what is over: a deadline
    /// already ticked off, an appointment that has already ended. The grid beside it still draws them,
    /// and the sheet that says how to read the list also says how much of it to read. Orbit.Web's
    /// calendar draws the same line.
    /// </summary>
    [Fact]
    public async Task What_is_over_is_left_off_the_list_until_it_is_asked_for()
    {
        using var context = new ScreenContext();
        // The clock says the fifteenth: the first has been and gone, the twentieth has not.
        await context.AddEventAsync("Over and done with", new DateTime(2026, 8, 1, 9, 0, 0));
        await context.AddEventAsync("Still coming", new DateTime(2026, 8, 20, 9, 0, 0));
        var screen = await context.OpenAsync();

        Assert.Equal(["Still coming"], Events(screen).Select(row => row.Title));
        // And the grid says the first still had something on it.
        Assert.True(screen.Days.Single(day => day.Date == new DateTime(2026, 8, 1)).HasEvents);

        screen.ShowsEverything = true;

        Assert.Equal(["Over and done with", "Still coming"], Events(screen).Select(row => row.Title));
    }

    /// <summary>
    /// A repeat that has stopped stops being drawn. Reading it as running forever is the mistake worth
    /// guarding against here: the rule carries the date it was told to end on.
    /// </summary>
    [Fact]
    public async Task A_repeat_that_has_ended_stops_being_drawn()
    {
        using var context = new ScreenContext();
        await context.AddEventAsync(
            "Standup", new DateTime(2026, 8, 3, 9, 0, 0),
            Weekly(until: new DateTime(2026, 8, 17)));
        var screen = await context.OpenAsync();
        // Counting the days the rule falls on, past ones included - see the test above.
        screen.ShowsEverything = true;

        Assert.Equal(3, Events(screen).Count(row => row.Title == "Standup"));
        Assert.False(screen.Days.Single(day => day.Date == new DateTime(2026, 8, 24)).HasEvents);
    }

    /// <summary>
    /// A month the repeat started long before still shows it. The rule is walked from where the event
    /// began, and stepping there one week at a time from an old start was the other way this could
    /// quietly go wrong.
    /// </summary>
    [Fact]
    public async Task A_repeat_that_started_long_ago_still_shows_this_month()
    {
        using var context = new ScreenContext();
        await context.AddEventAsync("Standup", new DateTime(2024, 1, 1, 9, 0, 0), Weekly());

        var screen = await context.OpenAsync();

        Assert.Contains(Events(screen), row => row.Title == "Standup");
    }

    /// <summary>
    /// Every occurrence is the same event: there is one to open, and editing it changes them all, which
    /// is what a rule means.
    /// </summary>
    [Fact]
    public async Task Opening_any_occurrence_opens_the_event_it_repeats()
    {
        using var context = new ScreenContext();
        await context.AddEventAsync("Standup", new DateTime(2026, 8, 3, 9, 0, 0), Weekly());
        var screen = await context.OpenAsync();

        var occurrences = Events(screen).Where(row => row.Title == "Standup").ToList();
        Assert.All(occurrences, row => Assert.Equal(occurrences[0].LocalId, row.LocalId));
    }

    private static RecurrenceDto Weekly(DateTime? until = null)
        => new("Weekly", 1, until is { } last
            ? new DateTimeOffset(last, TimeZoneInfo.Local.GetUtcOffset(last))
            : null);

    /// <summary>
    /// Choosing a day is what the phone has instead of the web's day view, so it has to be a way out of
    /// the year as well - otherwise a tap would narrow the list under a grid still showing twelve months.
    /// </summary>
    [Fact]
    public async Task Today_and_choosing_a_day_both_leave_the_year()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();

        await screen.ShowYearCommand.ExecuteAsync(null);
        await screen.ShowTodayCommand.ExecuteAsync(null);
        Assert.True(screen.IsShowingMonth);

        await screen.ShowYearCommand.ExecuteAsync(null);
        await screen.ChooseDayCommand.ExecuteAsync(screen.Days.First(day => day.IsInMonth));
        Assert.True(screen.IsShowingMonth);
        Assert.True(screen.IsShowingOneDay);
    }

    /// <summary>
    /// A deadline is as much a thing happening on a day as an appointment is. The phone's calendar
    /// showed only the appointments, so a week with three things due looked empty.
    /// </summary>
    [Fact]
    public async Task What_falls_due_is_shown_beside_the_events()
    {
        using var context = new ScreenContext();
        await context.AddDeadlineAsync("Groceries", "Buy milk", new DateTime(2026, 8, 20, 17, 0, 0));

        var screen = await context.OpenAsync();

        var deadline = Assert.Single(Deadlines(screen));
        Assert.Equal("Groceries: Buy milk", deadline.Label);
    }

    /// <summary>
    /// One list, whatever kind of thing is on it: two lists one under the other made the reader merge
    /// them by eye, in a period where they interleave by definition. Soonest first is what a calendar is
    /// asked for, so a deadline in the morning comes before an appointment in the afternoon.
    /// </summary>
    [Fact]
    public async Task Appointments_and_deadlines_are_read_as_one_list()
    {
        using var context = new ScreenContext();
        await context.AddEventAsync("Dentist", new DateTime(2026, 8, 20, 15, 0, 0));
        await context.AddDeadlineAsync("Groceries", "Buy milk", new DateTime(2026, 8, 19, 17, 0, 0));

        var screen = await context.OpenAsync();

        Assert.Equal(["Groceries: Buy milk", "Dentist"], screen.Listed.Select(entry => entry.Name));
        Assert.Collection(
            screen.Listed,
            entry => Assert.True(entry.IsDeadline),
            entry => Assert.True(entry.IsEvent));
    }

    /// <summary>
    /// The card's own menu, which is the only way something leaves the calendar without being opened
    /// first - see CalendarPage, where the question in front of it is asked.
    /// </summary>
    [Fact]
    public async Task Deleting_an_appointment_from_its_card_takes_it_off_the_calendar()
    {
        using var context = new ScreenContext();
        await context.AddEventAsync("Dentist", new DateTime(2026, 8, 20, 15, 0, 0));
        var screen = await context.OpenAsync();

        await screen.DeleteListedCommand.ExecuteAsync(Assert.Single(screen.Listed));

        Assert.Empty(screen.Listed);
    }

    /// <summary>
    /// A deadline has no row of its own to delete: it is one entry on a task list, and what the press
    /// does is take that entry off the list. The list itself stays, which is the part worth pinning
    /// down - deleting the list instead would be the same gesture doing something far larger.
    /// </summary>
    [Fact]
    public async Task Deleting_a_deadline_takes_the_entry_off_its_list_and_leaves_the_list()
    {
        using var context = new ScreenContext();
        await context.AddDeadlineAsync("Groceries", "Buy milk", new DateTime(2026, 8, 19, 17, 0, 0));
        var screen = await context.OpenAsync();

        await screen.DeleteListedCommand.ExecuteAsync(Assert.Single(screen.Listed));

        Assert.Empty(screen.Listed);
        Assert.Single(await context.TaskLists.GetAllAsync());
    }

    /// <summary>
    /// The three orders Orbit.Web reads the same list in. By type is for a reader who came looking for
    /// one kind of thing in a period holding a lot of both; within each kind it is still soonest first.
    /// </summary>
    [Fact]
    public async Task The_list_is_read_in_the_chosen_order()
    {
        using var context = new ScreenContext();
        await context.AddEventAsync("Zoo", new DateTime(2026, 8, 21, 9, 0, 0));
        await context.AddDeadlineAsync("Groceries", "Apples", new DateTime(2026, 8, 20, 17, 0, 0));
        var screen = await context.OpenAsync();

        Assert.Equal(["Groceries: Apples", "Zoo"], screen.Listed.Select(entry => entry.Name));

        screen.SortOrder = CalendarListSortOrder.Type;
        Assert.Equal(["Zoo", "Groceries: Apples"], screen.Listed.Select(entry => entry.Name));

        screen.SortOrder = CalendarListSortOrder.Alphabetical;
        Assert.Equal(["Groceries: Apples", "Zoo"], screen.Listed.Select(entry => entry.Name));
    }

    /// <summary>
    /// A calendar is read for what is coming, so the list leaves out what is over: an appointment that
    /// has ended, and a deadline that has been ticked off. The same line Orbit.Web draws over the same
    /// list - and the grid beside it still draws everything, because a day with something in it should
    /// say so whether or not it has been.
    /// </summary>
    [Fact]
    public async Task What_is_over_is_left_off_the_list()
    {
        using var context = new ScreenContext();
        // The clock this screen runs on says 2026-08-15; both days are in the month it opens on.
        await context.AddEventAsync("Last week", new DateTime(2026, 8, 10, 9, 0, 0));
        await context.AddEventAsync("Next week", new DateTime(2026, 8, 20, 9, 0, 0));

        var screen = await context.OpenAsync();

        Assert.Equal(["Next week"], screen.Listed.Select(entry => entry.Name));
        // Still on the grid: the day it happened is not an empty day.
        Assert.True(screen.Days.Single(day => day.Date == new DateTime(2026, 8, 10)).HasEvents);

        screen.ShowsEverything = true;
        Assert.Contains(screen.Listed, entry => entry.Name == "Last week");
    }

    /// <summary>
    /// A deadline that has passed and is still not done stays: it is the one thing on this page that
    /// most needs saying, and hiding it would hide the work.
    /// </summary>
    [Fact]
    public async Task An_overdue_deadline_stays_on_the_list()
    {
        using var context = new ScreenContext();
        // Due five days before the day the screen calls today, and still not ticked off.
        await context.AddDeadlineAsync("Groceries", "Buy milk", new DateTime(2026, 8, 10, 17, 0, 0));

        var screen = await context.OpenAsync();

        Assert.Equal(["Groceries: Buy milk"], screen.Listed.Select(entry => entry.Name));
    }

    [Fact]
    public async Task Whether_it_shows_everything_is_remembered_on_the_device()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();

        screen.ShowsEverything = true;

        Assert.True(context.ListOrder.Read().ShowsEverything);
        Assert.True((await context.OpenAsync()).ShowsEverything);
    }

    [Fact]
    public async Task The_chosen_order_is_remembered_on_the_device()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();

        screen.SortOrder = CalendarListSortOrder.Alphabetical;

        Assert.Equal(CalendarListSortOrder.Alphabetical, context.ListOrder.Read().SortOrder);
        Assert.Equal(CalendarListSortOrder.Alphabetical, (await context.OpenAsync()).SortOrder);
    }

    /// <summary>Whichever kind was pressed opens its own thing - see OpenListed.</summary>
    [Fact]
    public async Task Pressing_a_deadline_opens_the_list_it_sits_on()
    {
        using var context = new ScreenContext();
        var taskListId = await context.AddDeadlineAsync("Groceries", "Buy milk", new DateTime(2026, 8, 20, 17, 0, 0));
        var screen = await context.OpenAsync();

        screen.OpenListedCommand.Execute(Assert.Single(screen.Listed));

        Assert.Equal("ShowTaskList", context.Navigator.LastDestination);
        Assert.Equal(taskListId, context.Navigator.LastTaskListId);
    }

    /// <summary>The list beneath the grid follows the grid, deadlines as much as events.</summary>
    [Fact]
    public async Task Choosing_a_day_narrows_the_deadlines_to_it()
    {
        using var context = new ScreenContext();
        await context.AddDeadlineAsync("Groceries", "Buy milk", new DateTime(2026, 8, 20, 17, 0, 0));
        await context.AddDeadlineAsync("Trip", "Pack", new DateTime(2026, 8, 21, 17, 0, 0));
        var screen = await context.OpenAsync();

        await screen.ChooseDayCommand.ExecuteAsync(
            screen.Days.Single(day => day.Date == new DateTime(2026, 8, 20)));

        Assert.Equal("Groceries: Buy milk", Assert.Single(Deadlines(screen)).Label);
    }

    /// <summary>A day with something due on it is not an empty day, so the grid marks it.</summary>
    [Fact]
    public async Task A_day_with_something_due_is_marked_on_the_grid()
    {
        using var context = new ScreenContext();
        await context.AddDeadlineAsync("Groceries", "Buy milk", new DateTime(2026, 8, 20, 17, 0, 0));

        var screen = await context.OpenAsync();

        Assert.True(screen.Days.Single(day => day.Date == new DateTime(2026, 8, 20)).HasEvents);
    }

    /// <summary>
    /// An entry tied to an event is that event. Drawn on a day the event is already on, it is the same
    /// appointment written out twice, one line under the other.
    /// </summary>
    [Fact]
    public async Task An_entry_tied_to_an_event_is_not_drawn_again_under_it()
    {
        using var context = new ScreenContext();
        var eventId = await context.AddEventAsync("Dentist", new DateTime(2026, 8, 20, 9, 0, 0));
        await context.AddDeadlineAsync(
            "Saturday", "Dentist", new DateTime(2026, 8, 20, 9, 0, 0), tiedTo: eventId);

        var screen = await context.OpenAsync();

        Assert.Equal(["Dentist"], Events(screen).Select(row => row.Title));
        Assert.Empty(Deadlines(screen));
    }

    /// <summary>
    /// On any other day nothing stands for it, so it stays - hiding it there would lose the appointment
    /// rather than tidy it.
    /// </summary>
    [Fact]
    public async Task An_entry_due_on_a_day_its_event_is_not_on_still_shows()
    {
        using var context = new ScreenContext();
        var eventId = await context.AddEventAsync("Dentist", new DateTime(2026, 8, 20, 9, 0, 0));
        await context.AddDeadlineAsync(
            "Saturday", "Bring the forms", new DateTime(2026, 8, 19, 9, 0, 0), tiedTo: eventId);

        var screen = await context.OpenAsync();

        Assert.Equal("Saturday: Bring the forms", Assert.Single(Deadlines(screen)).Label);
    }

    /// <summary>
    /// Something to tick off opens the list it sits on, which is where it gets ticked. A checklist is
    /// the wrong landing for somewhere to get to, which is the other case below.
    /// </summary>
    [Fact]
    public async Task Opening_a_deadline_opens_the_list_it_sits_on()
    {
        using var context = new ScreenContext();
        var listId = await context.AddDeadlineAsync("Groceries", "Buy milk", new DateTime(2026, 8, 20, 17, 0, 0));
        var screen = await context.OpenAsync();

        screen.OpenDeadlineCommand.Execute(Assert.Single(Deadlines(screen)));

        Assert.Equal(listId, context.Navigator.LastTaskListId);
    }

    /// <summary>
    /// Somewhere to get to opens on its own, with what it is, when it is and where - the split
    /// Orbit.Web's calendar makes. The phone sent both to the checklist, which answers "where is it?"
    /// with a row of text and a tick box.
    /// </summary>
    [Fact]
    public async Task A_deadline_that_is_somewhere_opens_on_its_own()
    {
        using var context = new ScreenContext();
        var listId = await context.AddDeadlineAsync(
            "Errands", "Collect the parcel", new DateTime(2026, 8, 20, 17, 0, 0), at: "Długa 4, Gdańsk");
        var screen = await context.OpenAsync();

        screen.OpenDeadlineCommand.Execute(Assert.Single(Deadlines(screen)));

        var opened = Assert.NotNull(context.Navigator.LastTaskItem);
        Assert.Equal(listId, opened.TaskListLocalId);
    }

    /// <summary>Tied to an event is somewhere too - the event is where the address is kept.</summary>
    [Fact]
    public async Task A_deadline_tied_to_an_event_opens_on_its_own_as_well()
    {
        using var context = new ScreenContext();
        var eventId = await context.AddEventAsync("Dentist", new DateTime(2026, 8, 22, 9, 0, 0));
        // A day apart on purpose: on the event's own day the entry is left off, being the same
        // appointment already drawn there - see CalendarDeadline.From.
        await context.AddDeadlineAsync("Errands", "Dentist", new DateTime(2026, 8, 20, 17, 0, 0), tiedTo: eventId);
        var screen = await context.OpenAsync();

        screen.OpenDeadlineCommand.Execute(Assert.Single(Deadlines(screen)));

        Assert.NotNull(context.Navigator.LastTaskItem);
    }

    /// <summary>
    /// An event added from the box under the grid has to reach the server. It did not: the call that
    /// built it was positional, a priority was added to the shape beside two optional fields, and the
    /// notification channel's "None" slid into it - which the server refuses. Everything added here sat
    /// in the outbox behind a 400 that only the server's own log said out loud.
    /// </summary>
    [Fact]
    public async Task An_event_added_from_the_calendar_reaches_the_server()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();

        screen.NewEventTitle = "Dentist";
        await screen.AddEventCommand.ExecuteAsync(null);

        Assert.Contains(context.EventsOnTheServer, stored => stored.Details.Title == "Dentist");
    }

    /// <summary>
    /// The calendar gets out of the way as the list under it is read, and what survives is the week the
    /// reader is standing on - see MinimisedCalendar, and info/future-plan.md for why this is an Android
    /// job rather than a web one. A phone has one column and a thumb.
    /// </summary>
    [Fact]
    public async Task Minimising_the_calendar_leaves_the_week_being_looked_at()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        Assert.Equal(42, screen.Days.Count);

        screen.IsMinimised = true;

        Assert.Equal(7, screen.Days.Count);
        Assert.Contains(screen.Days, day => day.Date == context.Today);
    }

    /// <summary>The week the chosen day is in, when one has been chosen - that is what is being read.</summary>
    [Fact]
    public async Task The_week_that_survives_is_the_chosen_days()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        var chosen = screen.Days.Single(day => day.Date == new DateTime(2026, 8, 27));
        screen.ChooseDayCommand.Execute(chosen);

        screen.IsMinimised = true;

        Assert.Contains(screen.Days, day => day.Date == new DateTime(2026, 8, 27));
        Assert.DoesNotContain(screen.Days, day => day.Date == context.Today);
    }

    /// <summary>
    /// And comes back whole when the reader returns to the top of the list, without reading the store
    /// again: getting out of the way is a redraw, not a reload.
    /// </summary>
    [Fact]
    public async Task The_calendar_comes_back_whole()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();

        screen.IsMinimised = true;
        screen.IsMinimised = false;

        Assert.Equal(42, screen.Days.Count);
    }

    /// <summary>The year's answer to the same gesture: the month being read, not the twelve.</summary>
    [Fact]
    public async Task Minimising_the_year_leaves_the_month_being_read()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        await screen.ShowYearCommand.ExecuteAsync(null);
        Assert.Equal(12, screen.Months.Count);

        screen.IsMinimised = true;

        Assert.Equal("August", Assert.Single(screen.Months).Name);
    }

    /// <summary>
    /// The day's answer: one hour of the clock rather than the whole stretch worth drawing. The hour it
    /// is now, for a day that is today.
    /// </summary>
    [Fact]
    public async Task Minimising_a_day_leaves_the_hour_it_is_now()
    {
        using var context = new ScreenContext();
        await context.AddEventAsync("Standup", new DateTime(2026, 8, 15, 9, 0, 0));
        await context.AddEventAsync("Retro", new DateTime(2026, 8, 15, 16, 0, 0));
        var screen = await context.OpenAsync();
        screen.ChooseDayCommand.Execute(screen.Days.Single(day => day.Date == context.Today));

        // Nine to the hour the four o'clock ends in.
        Assert.Equal((9, 17), screen.HoursOnShow);

        screen.IsMinimised = true;

        // The hour the fake clock is at where this test runs, held inside what there is to draw.
        var nowLocal = context.Now.LocalDateTime.Hour;
        Assert.Equal((nowLocal, nowLocal), screen.HoursOnShow);
    }

    /// <summary>
    /// A day that is not today has no "now" to stand on, so what is left is the hour its first thing
    /// starts in - the alternative is an empty row above everything the day actually holds.
    /// </summary>
    [Fact]
    public async Task Minimising_another_day_leaves_the_hour_it_starts_at()
    {
        using var context = new ScreenContext();
        await context.AddEventAsync("Dentist", new DateTime(2026, 8, 20, 15, 0, 0));
        var screen = await context.OpenAsync();
        screen.ChooseDayCommand.Execute(screen.Days.Single(day => day.Date == new DateTime(2026, 8, 20)));

        screen.IsMinimised = true;

        Assert.Equal((15, 15), screen.HoursOnShow);
    }

    /// <summary>
    /// A month nobody is standing in - paged away from today, with no day chosen - keeps its first week
    /// rather than nothing. Whole weeks only: a grid is read by its rows.
    /// </summary>
    [Fact]
    public async Task Minimising_a_month_nobody_is_standing_in_keeps_its_first_week()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        await screen.ShowLaterCommand.ExecuteAsync(null);

        screen.IsMinimised = true;

        Assert.Equal(7, screen.Days.Count);
        Assert.Equal(screen.Days[0].Date, screen.Days.Min(day => day.Date));
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock;

        /// <summary>The day the screen calls today, which is the fake clock's rather than the machine's.</summary>
        public DateTime Today => _clock.GetUtcNow().LocalDateTime.Date;

        /// <summary>The moment the screen calls now - for the one test that is about the hour, not the day.</summary>
        public DateTimeOffset Now => _clock.GetUtcNow();
        private readonly FakeCalendarServer _server;
        private readonly LocalCalendarEventRepository _events;
        private readonly LocalTaskListRepository _taskLists;

        /// <summary>So a test can check what a deletion left behind on the list a deadline sat on.</summary>
        public LocalTaskListRepository TaskLists => _taskLists;
        private readonly CalendarEventSynchronizer _synchronizer;

        /// <param name="now">
        /// What the screen is to call now. Every test but one leaves it at the day this class was
        /// written around; the exception passes a different one to show the screen follows the clock it
        /// is given rather than the machine's.
        /// </param>
        public ScreenContext(string now = "2026-08-15T10:00:00Z")
        {
            _clock = new FakeTimeProvider(DateTimeOffset.Parse(now));
            _server = new FakeCalendarServer(_clock);
            _events = new LocalCalendarEventRepository(_localStore, _clock, FixedNetworkStatus.Online);
            _taskLists = new LocalTaskListRepository(_localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            _synchronizer = new CalendarEventSynchronizer(
                _localStore, new CalendarClient(_server.ToHttpClient()), _clock, new SyncGate(),
                new PendingCalendarLinkResolver(_clock, NullLogger<PendingCalendarLinkResolver>.Instance),
                NullLogger<CalendarEventSynchronizer>.Instance);
        }

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>What the fake server holds - for a test about whether a save actually arrived.</summary>
        public IReadOnlyCollection<CalendarEventDto> EventsOnTheServer => _server.Events;

        /// <summary>A task entry falling due, which the calendar shows beside the events.</summary>
        /// <param name="tiedTo">The event this entry is the same appointment as, when it is one.</param>
        /// <param name="at">Where it happens, for an entry carrying an address of its own.</param>
        public async Task<Guid> AddDeadlineAsync(
            string listTitle, string description, DateTime localDue, Guid? tiedTo = null, string at = "")
        {
            var due = new DateTimeOffset(localDue, TimeZoneInfo.Local.GetUtcOffset(localDue)).ToUniversalTime();
            var created = await _taskLists.CreateAsync(listTitle,
            [
                new(Guid.NewGuid(), description, due, false, null, "None", false, "None", new TimeOnly(9, 0),
                    "Checklist", at, tiedTo)
            ]);

            return created.LocalId;
        }

        /// <summary>
        /// Returns the id the server would know it by, which is what an entry tied to it points at. The
        /// event is stamped as already synced: one that has never left the phone is not something
        /// anything else can point at yet.
        /// </summary>
        /// <param name="repeating">The rule this event repeats by, when it repeats - see CalendarOccurrences.</param>
        public async Task<Guid> AddEventAsync(
            string title, DateTime localStart, RecurrenceDto? repeating = null)
        {
            var start = new DateTimeOffset(localStart, TimeZoneInfo.Local.GetUtcOffset(localStart)).ToUniversalTime();
            var created = await _events.CreateAsync(
                new CalendarEventDetailsDto(
                    title, null, null, null, start, start.AddHours(1), false, repeating, [], [], ReminderNotificationChannel: "None"));

            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.CalendarEvents.Single(candidate => candidate.LocalId == created.LocalId);
            stored.ServerId = Guid.NewGuid();
            await dbContext.SaveChangesAsync();
            return stored.ServerId.Value;
        }

        /// <summary>What order the list is read in, kept across the screens one test opens.</summary>
        public InMemoryCalendarListOrderStore ListOrder { get; } = new();

        public async Task<CalendarViewModel> OpenAsync()
        {
            var screen = new CalendarViewModel(
                _events, _synchronizer, FixedNetworkStatus.Online, _clock, new SyncState(FixedNetworkStatus.Online, _clock),
                Navigator, new Translations(new InMemoryLanguageStore()), _taskLists, ListOrder);

            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public void Dispose()
        {
            _server.Dispose();
            _localStore.Dispose();
        }
    }

    /// <summary>
    /// The browser's third view, which the phone had no way to reach: a month grid whose days could be
    /// tapped is not the same as being able to ask for today. Somebody looking for "just today" had to
    /// find it in the grid first.
    /// </summary>
    [Fact]
    public async Task One_day_can_be_asked_for_on_its_own()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();

        await screen.ShowDayCommand.ExecuteAsync(null);

        Assert.True(screen.IsShowingDay);
        Assert.False(screen.IsShowingMonth);
        Assert.Equal(context.Today, screen.SelectedDay);
    }

    /// <summary>It opens on whichever day was chosen in the grid, not on today regardless.</summary>
    [Fact]
    public async Task The_day_view_opens_on_the_day_that_was_chosen()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        var chosen = screen.Days.First(day => day.IsInMonth);
        await screen.ChooseDayCommand.ExecuteAsync(chosen);

        await screen.ShowDayCommand.ExecuteAsync(null);

        Assert.Equal(chosen.Date, screen.SelectedDay);
    }

    /// <summary>
    /// A step means one of whatever is on screen. Stepping a whole month while showing one day read as
    /// the arrows being broken.
    /// </summary>
    [Fact]
    public async Task Stepping_in_the_day_view_moves_by_a_day()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        await screen.ShowDayCommand.ExecuteAsync(null);

        await screen.ShowLaterCommand.ExecuteAsync(null);

        Assert.Equal(context.Today.AddDays(1), screen.SelectedDay);
    }

    [Fact]
    public async Task Stepping_in_the_month_view_still_moves_by_a_month()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        var wasShowing = screen.Month;

        await screen.ShowLaterCommand.ExecuteAsync(null);

        Assert.Equal(wasShowing.AddMonths(1).Month, screen.Month.Month);
    }

    [Fact]
    public async Task Going_back_to_the_month_leaves_the_day_view()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        await screen.ShowDayCommand.ExecuteAsync(null);

        await screen.ShowMonthCommand.ExecuteAsync(null);

        Assert.True(screen.IsShowingMonth);
        Assert.False(screen.IsShowingDay);
    }
}

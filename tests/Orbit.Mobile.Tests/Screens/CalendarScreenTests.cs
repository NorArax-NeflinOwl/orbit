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
    [Fact]
    public async Task The_month_is_what_opens()
    {
        using var context = new ScreenContext();

        var screen = await context.OpenAsync();

        Assert.True(screen.IsShowingMonth);
        Assert.False(screen.IsShowingYear);
        Assert.Equal("August 2026", screen.PeriodLabel);
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

        Assert.Equal(["Dentist"], screen.Events.Select(row => row.Title));

        await screen.ShowYearCommand.ExecuteAsync(null);
        Assert.Equal(["Dentist", "Concert"], screen.Events.Select(row => row.Title));

        await screen.ShowMonthCommand.ExecuteAsync(null);
        Assert.Equal(["Dentist"], screen.Events.Select(row => row.Title));
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

        // Five Mondays in August 2026, counting the one it starts on.
        Assert.Equal(5, screen.Events.Count(row => row.Title == "Standup"));
        foreach (var monday in new[] { 3, 10, 17, 24, 31 })
        {
            Assert.True(screen.Days.Single(day => day.Date == new DateTime(2026, 8, monday)).HasEvents);
        }
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

        Assert.Equal(3, screen.Events.Count(row => row.Title == "Standup"));
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

        Assert.NotEmpty(screen.Events.Where(row => row.Title == "Standup"));
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

        var occurrences = screen.Events.Where(row => row.Title == "Standup").ToList();
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

        var deadline = Assert.Single(screen.Deadlines);
        Assert.Equal("Groceries: Buy milk", deadline.Label);
        Assert.True(screen.HasDeadlines);
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

        Assert.Equal("Groceries: Buy milk", Assert.Single(screen.Deadlines).Label);
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

        Assert.Equal(["Dentist"], screen.Events.Select(row => row.Title));
        Assert.Empty(screen.Deadlines);
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

        Assert.Equal("Saturday: Bring the forms", Assert.Single(screen.Deadlines).Label);
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

        screen.OpenDeadlineCommand.Execute(Assert.Single(screen.Deadlines));

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

        screen.OpenDeadlineCommand.Execute(Assert.Single(screen.Deadlines));

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

        screen.OpenDeadlineCommand.Execute(Assert.Single(screen.Deadlines));

        Assert.NotNull(context.Navigator.LastTaskItem);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-15T10:00:00Z"));
        private readonly FakeCalendarServer _server;
        private readonly LocalCalendarEventRepository _events;
        private readonly LocalTaskListRepository _taskLists;
        private readonly CalendarEventSynchronizer _synchronizer;

        public ScreenContext()
        {
            _server = new FakeCalendarServer(_clock);
            _events = new LocalCalendarEventRepository(_localStore, _clock, FixedNetworkStatus.Online);
            _taskLists = new LocalTaskListRepository(_localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            _synchronizer = new CalendarEventSynchronizer(
                _localStore, new CalendarClient(_server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<CalendarEventSynchronizer>.Instance);
        }

        public RecordingScreenNavigator Navigator { get; } = new();

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
                    title, null, null, null, start, start.AddHours(1), false, repeating, [], [], "None", "None"));

            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.CalendarEvents.Single(candidate => candidate.LocalId == created.LocalId);
            stored.ServerId = Guid.NewGuid();
            await dbContext.SaveChangesAsync();
            return stored.ServerId.Value;
        }

        public async Task<CalendarViewModel> OpenAsync()
        {
            var screen = new CalendarViewModel(
                _events, _synchronizer, FixedNetworkStatus.Online, _clock, new SyncState(FixedNetworkStatus.Online, _clock),
                Navigator, new Translations(new InMemoryLanguageStore()), _taskLists);

            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public void Dispose()
        {
            _server.Dispose();
            _localStore.Dispose();
        }
    }
}

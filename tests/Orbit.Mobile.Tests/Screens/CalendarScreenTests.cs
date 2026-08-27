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

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-15T10:00:00Z"));
        private readonly FakeCalendarServer _server;
        private readonly LocalCalendarEventRepository _events;
        private readonly CalendarEventSynchronizer _synchronizer;

        public ScreenContext()
        {
            _server = new FakeCalendarServer(_clock);
            _events = new LocalCalendarEventRepository(_localStore, _clock, FixedNetworkStatus.Online);
            _synchronizer = new CalendarEventSynchronizer(
                _localStore, new CalendarClient(_server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<CalendarEventSynchronizer>.Instance);
        }

        public Task AddEventAsync(string title, DateTime localStart)
        {
            var start = new DateTimeOffset(localStart, TimeZoneInfo.Local.GetUtcOffset(localStart)).ToUniversalTime();

            return _events.CreateAsync(
                new CalendarEventDetailsDto(title, null, null, null, start, start.AddHours(1), false, null, [], [], "None", "None"));
        }

        public async Task<CalendarViewModel> OpenAsync()
        {
            var screen = new CalendarViewModel(
                _events, _synchronizer, FixedNetworkStatus.Online, _clock, new SyncState(FixedNetworkStatus.Online, _clock),
                new RecordingScreenNavigator(), new Translations(new InMemoryLanguageStore()));

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

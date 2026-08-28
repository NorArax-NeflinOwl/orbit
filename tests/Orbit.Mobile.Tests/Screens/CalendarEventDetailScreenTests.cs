using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Calendar;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The screen an event opens into. It was written narrower than Orbit.Web's editor on purpose - guests,
/// recurrence and the rest travel through untouched rather than being dropped - but the parts it does
/// show have to survive an edit, and the end of a multi-day event did not.
/// </summary>
public sealed class CalendarEventDetailScreenTests
{
    /// <summary>
    /// The end used to be built from the start's date, so editing the title of a three-day event turned
    /// it into a one-day event - a quiet deletion of exactly the kind this screen was built to avoid.
    /// </summary>
    [Fact]
    public async Task An_event_spanning_days_still_spans_them_after_an_unrelated_edit()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(
            new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 22, 17, 0, 0));
        var screen = await context.OpenAsync(stored.LocalId);

        screen.Title = "Conference";
        await screen.SaveCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(stored.LocalId);
        Assert.Equal(new DateTime(2026, 8, 20), reopened.StartDate);
        Assert.Equal(new DateTime(2026, 8, 22), reopened.EndDate);
    }

    /// <summary>
    /// An all-day event ends at midnight the next day on the wire, which reads as one day too many on a
    /// picker - so the screen shows the last day it covers, and puts the midnight back on the way out.
    /// </summary>
    [Fact]
    public async Task An_all_day_event_shows_the_last_day_it_covers()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(
            new DateTime(2026, 8, 20), new DateTime(2026, 8, 21), isAllDay: true);
        var screen = await context.OpenAsync(stored.LocalId);

        Assert.Equal(new DateTime(2026, 8, 20), screen.EndDate);

        await screen.SaveCommand.ExecuteAsync(null);
        var reopened = await context.OpenAsync(stored.LocalId);
        Assert.Equal(new DateTime(2026, 8, 20), reopened.EndDate);
    }

    /// <summary>
    /// A place is a point with an optional label - see EventLocationDto - so a name typed with nothing
    /// behind it is not a location, and Orbit.Web does not send one either.
    /// </summary>
    [Fact]
    public async Task A_name_with_no_point_behind_it_is_not_a_location()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0));
        var screen = await context.OpenAsync(stored.LocalId);

        screen.LocationAddress = "The pub";
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.False(screen.HasLocation);
        Assert.Null((await context.FindAsync(stored.LocalId)).Details.Location);
    }

    [Fact]
    public async Task The_phone_can_say_where_it_is_and_take_it_back()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0));
        var screen = await context.OpenAsync(stored.LocalId);

        await screen.UseMyLocationCommand.ExecuteAsync(null);

        Assert.True(screen.HasLocation);
        var saved = (await context.FindAsync(stored.LocalId)).Details.Location;
        Assert.Equal(52.2297, saved!.Latitude, precision: 4);
        // Reverse geocoding filled the name in, because nothing had been typed.
        Assert.Contains("Marszałkowska", screen.LocationAddress);

        await screen.RemoveLocationCommand.ExecuteAsync(null);
        Assert.False(screen.HasLocation);
        Assert.Null((await context.FindAsync(stored.LocalId)).Details.Location);
    }

    /// <summary>A refusal and an empty reading read the same to somebody standing there: no place.</summary>
    [Fact]
    public async Task A_position_the_phone_cannot_give_is_said_rather_than_guessed()
    {
        using var context = new ScreenContext();
        context.Here.Reading = new DeviceLocationResult(DeviceLocationOutcome.NotPermitted);
        var stored = await context.AddEventAsync(new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0));
        var screen = await context.OpenAsync(stored.LocalId);

        await screen.UseMyLocationCommand.ExecuteAsync(null);

        Assert.False(screen.HasLocation);
        Assert.NotEmpty(screen.Status);
    }

    /// <summary>
    /// A rule the phone could not set, only carry. "Until" is sent as the end of that day, so a rule
    /// that repeats until the 20th includes the 20th - which is what picking that date means.
    /// </summary>
    [Fact]
    public async Task A_repeat_can_be_set_and_taken_off_again()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0));
        var screen = await context.OpenAsync(stored.LocalId);

        screen.IsRecurring = true;
        screen.RecurrenceFrequency = "Monthly";
        screen.RecurrenceIntervalCount = 2;
        screen.RecurrenceEnds = true;
        screen.RecurrenceUntil = new DateTime(2026, 12, 20);
        await screen.SaveCommand.ExecuteAsync(null);

        var saved = (await context.FindAsync(stored.LocalId)).Details.Recurrence;
        Assert.Equal("Monthly", saved!.Frequency);
        Assert.Equal(2, saved.IntervalCount);
        Assert.Equal(new DateTime(2026, 12, 20), saved.UntilUtc!.Value.ToLocalTime().Date);

        screen.IsRecurring = false;
        await screen.SaveCommand.ExecuteAsync(null);
        Assert.Null((await context.FindAsync(stored.LocalId)).Details.Recurrence);
    }

    /// <summary>A repeat with no end is the web's blank "until", not a missing rule.</summary>
    [Fact]
    public async Task A_repeat_without_an_end_repeats_without_one()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0));
        var screen = await context.OpenAsync(stored.LocalId);

        screen.IsRecurring = true;
        await screen.SaveCommand.ExecuteAsync(null);

        var saved = (await context.FindAsync(stored.LocalId)).Details.Recurrence;
        Assert.NotNull(saved);
        Assert.Null(saved!.UntilUtc);
    }

    /// <summary>
    /// The rule the browser set has to come back the same, because this screen reads it into its own
    /// fields now rather than carrying the whole thing past untouched.
    /// </summary>
    [Fact]
    public async Task A_rule_set_elsewhere_is_read_back_as_it_was()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(
            new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0),
            recurrence: new RecurrenceDto("Daily", 3, null));

        var screen = await context.OpenAsync(stored.LocalId);

        Assert.True(screen.IsRecurring);
        Assert.Equal("Daily", screen.RecurrenceFrequency);
        Assert.Equal(3, screen.RecurrenceIntervalCount);
        Assert.False(screen.RecurrenceEnds);
    }

    [Fact]
    public async Task Reminders_can_be_added_and_taken_off()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0));
        var screen = await context.OpenAsync(stored.LocalId);

        screen.ReminderToAdd = screen.ReminderChoices.Single(choice => choice.MinutesBefore == 60);
        screen.ReminderToAdd = screen.ReminderChoices.Single(choice => choice.MinutesBefore == 10);

        // Kept in the order they happen, not the order they were picked.
        Assert.Equal([10, 60], (await context.FindAsync(stored.LocalId)).Details.ReminderMinutesBeforeStart);

        await screen.RemoveReminderCommand.ExecuteAsync(screen.Reminders[0]);
        Assert.Equal([60], (await context.FindAsync(stored.LocalId)).Details.ReminderMinutesBeforeStart);
    }

    /// <summary>Two of the same sentence at the same moment is not two reminders.</summary>
    [Fact]
    public async Task The_same_reminder_twice_is_still_one_reminder()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0));
        var screen = await context.OpenAsync(stored.LocalId);

        screen.ReminderToAdd = screen.ReminderChoices.Single(choice => choice.MinutesBefore == 60);
        screen.ReminderToAdd = screen.ReminderChoices.Single(choice => choice.MinutesBefore == 60);

        Assert.Single(screen.Reminders);
    }

    /// <summary>
    /// Orbit.Web offers a custom number of minutes as well as its presets, so a value from there need
    /// not be one of ours - and saying "80 min before" beats dropping a reminder somebody set.
    /// </summary>
    [Fact]
    public async Task A_reminder_that_is_not_one_of_the_presets_is_still_shown()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(
            new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0), reminderMinutes: [80]);

        var screen = await context.OpenAsync(stored.LocalId);

        Assert.Equal(80, screen.Reminders.Single().MinutesBefore);
        Assert.Contains("80", screen.Reminders.Single().Name);
    }

    [Fact]
    public async Task How_it_is_announced_can_be_chosen()
    {
        using var context = new ScreenContext();
        var stored = await context.AddEventAsync(new DateTime(2026, 8, 20, 9, 0, 0), new DateTime(2026, 8, 20, 10, 0, 0));
        var screen = await context.OpenAsync(stored.LocalId);

        screen.ReminderChannel = screen.Channels.Single(channel => channel.Value == "Both");
        await screen.SaveCommand.ExecuteAsync(null);

        Assert.Equal("Both", (await context.FindAsync(stored.LocalId)).Details.ReminderNotificationChannel);
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

        public FixedDeviceLocation Here { get; } = new();

        public Task<LocalCalendarEvent> AddEventAsync(
            DateTime localStart, DateTime localEnd, bool isAllDay = false, RecurrenceDto? recurrence = null,
            IReadOnlyList<int>? reminderMinutes = null)
        {
            var start = new DateTimeOffset(localStart, TimeZoneInfo.Local.GetUtcOffset(localStart)).ToUniversalTime();
            var end = new DateTimeOffset(localEnd, TimeZoneInfo.Local.GetUtcOffset(localEnd)).ToUniversalTime();

            return _events.CreateAsync(
                new CalendarEventDetailsDto(
                    "Standup", null, null, null, start, end, isAllDay, recurrence, [],
                    reminderMinutes ?? [], "None", "None"));
        }

        public async Task<LocalCalendarEvent> FindAsync(Guid localId)
            => (await _events.FindAsync(localId))!;

        public async Task<CalendarEventDetailViewModel> OpenAsync(Guid localId)
        {
            var screen = new CalendarEventDetailViewModel(
                _events, _synchronizer, new Translations(new InMemoryLanguageStore()),
                ShareTestPanel.For(_localStore, new ChatRepository(_localStore, _clock)),
                new RecordingScreenNavigator(),
                new CalendarClient(_server.ToHttpClient()),
                new EditLock(FixedNetworkStatus.Online, _clock, new Translations(new InMemoryLanguageStore())),
                Here);

            screen.Open(localId);
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

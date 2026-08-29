using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Calendar;

/// <summary>
/// Calendar events on the sync spine - the third entity type. Fewer tests than task lists on purpose:
/// the rules they would re-check (queue order, retry classification, not overwriting unsent work, two
/// runs not overlapping) are the spine's, tested once where they live, and re-testing them per feature
/// would grow with every feature while catching nothing new. What is checked here is what a calendar
/// event does differently - its own round trip, its own ordering, and the details block surviving.
/// </summary>
public sealed class CalendarEventSyncTests
{
    [Fact]
    public async Task An_event_written_offline_reaches_the_server_when_the_connection_returns()
    {
        using var context = new CalendarContext();
        context.GoOffline();
        await context.Events.CreateAsync(FakeCalendarServer.DetailsFor("Dentist", context.Clock.GetUtcNow()));

        context.ComeBackOnline();
        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.Sent);
        Assert.Contains(context.Server.Events, item => item.Details.Title == "Dentist");
    }

    [Fact]
    public async Task The_whole_details_block_survives_the_round_trip()
    {
        using var context = new CalendarContext();
        var start = context.Clock.GetUtcNow().AddDays(3);
        var details = FakeCalendarServer.DetailsFor("Standup", start) with
        {
            Description = "Daily",
            Location = new Orbit.Contracts.Calendar.EventLocationDto("Office", 52.23, 21.01),
            ReminderMinutesBeforeStart = [10, 60]
        };

        await context.Events.CreateAsync(details);
        await context.SynchroniseAsync();

        // It is stored as one JSON column and sent as one request shape, so losing a field here would be
        // silent everywhere else.
        var onServer = context.Server.Events.Single(item => item.Details.Title == "Standup").Details;
        Assert.Equal("Daily", onServer.Description);
        Assert.Equal("Office", onServer.Location!.Address);
        Assert.Equal([10, 60], onServer.ReminderMinutesBeforeStart);
    }

    [Fact]
    public async Task Times_leave_the_phone_as_UTC_whatever_offset_they_were_written_with()
    {
        using var context = new CalendarContext();
        var localStart = new DateTimeOffset(2026, 8, 26, 9, 0, 0, TimeSpan.FromHours(2));

        await context.Events.CreateAsync(FakeCalendarServer.DetailsFor("Dentist", localStart));
        await context.SynchroniseAsync();

        // Npgsql refuses a DateTimeOffset with a non-zero offset for a "timestamp with time zone" column
        // outright, and answers with a 500 that looks nothing like a client mistake. These times travel
        // inside a JSON block, so the store's own UTC normalisation never sees them.
        var sent = context.Server.Events.Single(item => item.Details.Title == "Dentist").Details;
        Assert.Equal(TimeSpan.Zero, sent.StartUtc.Offset);
        Assert.Equal(localStart.UtcDateTime, sent.StartUtc.UtcDateTime);
    }

    [Fact]
    public async Task Events_are_listed_soonest_first()
    {
        using var context = new CalendarContext();
        var now = context.Clock.GetUtcNow();
        await context.Events.CreateAsync(FakeCalendarServer.DetailsFor("Later", now.AddDays(5)));
        await context.Events.CreateAsync(FakeCalendarServer.DetailsFor("Sooner", now.AddDays(1)));

        var events = await context.Events.GetAllAsync();

        // The start time lives inside the JSON block, so this ordering is done in memory rather than in
        // SQL - a detail worth pinning, because it is the kind of thing a later refactor would "fix".
        Assert.Equal(["Sooner", "Later"], events.Select(item => item.Details.Title));
    }

    [Fact]
    public async Task An_event_written_elsewhere_appears_on_the_phone()
    {
        using var context = new CalendarContext();
        context.Server.AddEvent("Written elsewhere");

        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.Received);
        Assert.Equal("Written elsewhere", (await context.Events.GetAllAsync()).Single().Details.Title);
    }

    [Fact]
    public async Task An_event_deleted_elsewhere_leaves_the_phone_too()
    {
        using var context = new CalendarContext();
        var remote = context.Server.AddEvent("Cancelled");
        await context.SynchroniseAsync();

        context.Clock.Advance(TimeSpan.FromMinutes(1));
        context.Server.DeleteEvent(remote.Id);
        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.RemovedLocally);
        Assert.Empty(await context.Events.GetAllAsync());
    }

    [Fact]
    public async Task An_event_somebody_else_can_change_is_not_editable_offline()
    {
        using var context = new CalendarContext(online: false);
        context.Server.AddEvent("Shared", isSharedWithOthers: true);
        await context.SynchroniseAsync();

        var stored = (await context.Events.GetAllAsync()).Single();

        Assert.False(await context.Events.CanEditAsync(stored.LocalId));
        Assert.Equal(
            LocalWriteOutcome.RefusedWhileOffline,
            await context.Events.UpdateAsync(stored.LocalId, stored.Details with { Title = "Edited anyway" }));
    }

    private sealed class CalendarContext : IDisposable
    {
        private readonly LocalStore _localStore = new();

        public CalendarContext(bool online = true)
        {
            Clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
            Server = new FakeCalendarServer(Clock);
            Events = new LocalCalendarEventRepository(
                _localStore, Clock, online ? FixedNetworkStatus.Online : FixedNetworkStatus.Offline);
            Synchronizer = new CalendarEventSynchronizer(
                _localStore, new CalendarClient(Server.ToHttpClient()), Clock, new SyncGate(),
                NullLogger<CalendarEventSynchronizer>.Instance);
        }

        public FakeTimeProvider Clock { get; }
        public FakeCalendarServer Server { get; }
        public LocalCalendarEventRepository Events { get; }
        public CalendarEventSynchronizer Synchronizer { get; }

        public Task<SyncResult> SynchroniseAsync() => Synchronizer.SynchroniseAsync(CancellationToken.None);

        public void GoOffline() => Server.IsUnreachable = true;

        public void ComeBackOnline() => Server.IsUnreachable = false;

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Contracts.Tasks;
using Orbit.Core.Tasks;
using Microsoft.EntityFrameworkCore;
using Orbit.Core.Sync;
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
    /// <summary>
    /// A create the server will not have is dropped and said out loud, rather than queued for ever. It
    /// used to throw: the exception was not one the outbox retries, so it escaped the replay altogether,
    /// the change stayed queued, and every change behind it stopped moving. On a phone that reads as
    /// "couldn't sync" with nothing else to go on - which is exactly how it was found.
    /// </summary>
    [Fact]
    public async Task An_event_the_server_refuses_is_given_up_on_rather_than_queued_for_ever()
    {
        using var context = new CalendarContext();
        // A priority nothing answers to, which is what the calendar's own box used to send.
        await context.Events.CreateAsync(
            FakeCalendarServer.DetailsFor("Dentist", context.Clock.GetUtcNow()) with { Priority = "None" });

        var result = await context.SynchroniseAsync();

        Assert.Equal(0, result.Sent);
        Assert.Equal(1, result.GivenUp);
        // And the reader is told, because this is their work being thrown away - see OutboxReplay.
        Assert.Contains(context.DroppedNotices(), kind => kind == "ChangeDropped");

        // Nothing is left blocking the queue: the next event goes.
        await context.Events.CreateAsync(
            FakeCalendarServer.DetailsFor("Standup", context.Clock.GetUtcNow()));
        var afterwards = await context.SynchroniseAsync();
        Assert.Equal(1, afterwards.Sent);
    }

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

    /// <summary>
    /// The other half of an appointment made with no connection: once the event is named, the entry that
    /// stands for it has to carry that name, and the list has to be queued so the server hears about it.
    /// Without this the appointment would exist on both sides and be joined on neither.
    /// </summary>
    [Fact]
    public async Task An_appointment_made_offline_is_joined_to_its_entry_when_the_event_is_named()
    {
        using var context = new CalendarContext();
        context.GoOffline();
        var madeHere = await context.Events.CreateAsync(
            FakeCalendarServer.DetailsFor("Dentist", context.Clock.GetUtcNow()));
        var entryId = await context.AddCalendarEntryWaitingForAsync(madeHere.LocalId);

        context.ComeBackOnline();
        await context.SynchroniseAsync();

        var entry = await context.FindEntryAsync(entryId);
        var named = Assert.Single(context.Server.Events, item => item.Details.Title == "Dentist");
        Assert.Equal(named.Id, entry.LinkedCalendarEventId);
        Assert.Empty(await context.PendingLinksAsync());
        // Queued as well as joined: the id is new to the server, and nothing else would carry it up.
        Assert.True(await context.HasQueuedTheListAsync());
    }

    /// <summary>
    /// An entry that stopped being an appointment while the phone was offline. The link is stale rather
    /// than pending: it is dropped, and the event stays in the calendar for the reader to deal with -
    /// the same thing that happens when the kind is changed online.
    /// </summary>
    [Fact]
    public async Task An_entry_that_stopped_being_an_appointment_is_let_go_of_rather_than_joined()
    {
        using var context = new CalendarContext();
        context.GoOffline();
        var madeHere = await context.Events.CreateAsync(
            FakeCalendarServer.DetailsFor("Dentist", context.Clock.GetUtcNow()));
        var entryId = await context.AddCalendarEntryWaitingForAsync(madeHere.LocalId);
        await context.TurnTheEntryIntoAnErrandAsync(entryId);

        context.ComeBackOnline();
        await context.SynchroniseAsync();

        var entry = await context.FindEntryAsync(entryId);
        Assert.Null(entry.LinkedCalendarEventId);
        Assert.Empty(await context.PendingLinksAsync());
        Assert.Contains(context.Server.Events, item => item.Details.Title == "Dentist");
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
                new PendingCalendarLinkResolver(Clock, NullLogger<PendingCalendarLinkResolver>.Instance),
                NullLogger<CalendarEventSynchronizer>.Instance);
        }

        public FakeTimeProvider Clock { get; }
        public FakeCalendarServer Server { get; }
        public LocalCalendarEventRepository Events { get; }
        public CalendarEventSynchronizer Synchronizer { get; }

        public Task<SyncResult> SynchroniseAsync() => Synchronizer.SynchroniseAsync(CancellationToken.None);

        /// <summary>What the phone wrote into its own feed about work it gave up on - see OutboxReplay.</summary>
        public IReadOnlyList<string> DroppedNotices()
        {
            using var dbContext = _localStore.CreateDbContext();
            return [.. dbContext.Notifications.Select(notification => notification.Kind)];
        }

        public void GoOffline() => Server.IsUnreachable = true;

        public void ComeBackOnline() => Server.IsUnreachable = false;

        /// <summary>A list holding one Calendar entry that stands for an event this phone has just made.</summary>
        public async Task<Guid> AddCalendarEntryWaitingForAsync(Guid calendarEventLocalId)
        {
            var listLocalId = Guid.NewGuid();
            var entryId = Guid.NewGuid();
            await using var dbContext = _localStore.CreateDbContext();
            dbContext.TaskLists.Add(new LocalTaskList
            {
                LocalId = listLocalId,
                ServerId = Guid.NewGuid(),
                Title = "Saturday",
                Items = [new TaskItemDto(
                    entryId, "dentist", null, false, null, "Push", false, "Push", new TimeOnly(9, 0),
                    nameof(TaskItemKind.Calendar))],
                CreatedAtUtc = Clock.GetUtcNow(),
                UpdatedAtUtc = Clock.GetUtcNow()
            });

            dbContext.PendingCalendarLinks.Add(new PendingCalendarLink
            {
                CalendarEventLocalId = calendarEventLocalId,
                TaskListLocalId = listLocalId,
                Description = "dentist"
            });

            await dbContext.SaveChangesAsync();
            return entryId;
        }

        public async Task TurnTheEntryIntoAnErrandAsync(Guid entryId)
        {
            await using var dbContext = _localStore.CreateDbContext();
            var list = dbContext.TaskLists.Single();
            list.Items = [.. list.Items.Select(item => item.Id == entryId
                ? item with { Kind = nameof(TaskItemKind.Checklist) }
                : item)];

            await dbContext.SaveChangesAsync();
        }

        public async Task<TaskItemDto> FindEntryAsync(Guid entryId)
        {
            await using var dbContext = _localStore.CreateDbContext();
            return dbContext.TaskLists.Single().Items.Single(item => item.Id == entryId);
        }

        public async Task<IReadOnlyList<PendingCalendarLink>> PendingLinksAsync()
        {
            await using var dbContext = _localStore.CreateDbContext();
            return await dbContext.PendingCalendarLinks.ToListAsync();
        }

        public async Task<bool> HasQueuedTheListAsync()
        {
            await using var dbContext = _localStore.CreateDbContext();
            return await dbContext.Outbox.AnyAsync(entry => entry.EntityType == SyncEntityType.TaskList);
        }

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Calendar;
using Orbit.Core.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Brings calendar events and the server back into step. The third entity type on the spine, and the
/// one that shows what the factoring bought: replaying the queue, classifying failures, holding the
/// cursor and stopping runs overlapping are all shared, so what is written here is only what a calendar
/// event genuinely does differently - which turns out to be its request shapes and one field.
/// </summary>
public sealed class CalendarEventSynchronizer
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly CalendarClient _calendarClient;
    private readonly TimeProvider _timeProvider;
    private readonly SyncGate _syncGate;
    private readonly PendingCalendarLinkResolver _pendingLinks;
    private readonly ILogger<CalendarEventSynchronizer> _logger;

    public CalendarEventSynchronizer(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, CalendarClient calendarClient,
        TimeProvider timeProvider, SyncGate syncGate, PendingCalendarLinkResolver pendingLinks,
        ILogger<CalendarEventSynchronizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _calendarClient = calendarClient;
        _timeProvider = timeProvider;
        _syncGate = syncGate;
        _pendingLinks = pendingLinks;
        _logger = logger;
    }

    /// <summary>Never throws for being offline - see NoteSynchronizer for why that is a rule here.</summary>
    public Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
        => _syncGate.RunAsync(SyncEntityType.CalendarEvent, () => RunAsync(cancellationToken), cancellationToken);

    private async Task<SyncResult> RunAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var push = await OutboxReplay.RunAsync(
            dbContext, SyncEntityType.CalendarEvent,
            (entry, token) => SendAsync(dbContext, entry, token), _timeProvider, _logger, cancellationToken);

        // Straight after the push, because that is where a locally-made event gains its server id -
        // and an appointment made offline is only half made until its entry carries that id.
        await _pendingLinks.ResolveAsync(dbContext, cancellationToken);

        try
        {
            var pull = await PullChangesAsync(dbContext, cancellationToken);
            return new SyncResult(push.Sent, pull.Received, pull.RemovedLocally, push.GivenUp, ReachedTheServer: true);
        }
        catch (Exception exception) when (SyncFailure.IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull calendar events ({Reason})", exception.Message);
            return push.Sent > 0
                ? new SyncResult(push.Sent, 0, 0, push.GivenUp, ReachedTheServer: true)
                : SyncResult.NeverGotThrough(push.GivenUp);
        }
    }

    private async Task<SendResult> SendAsync(OrbitLocalDbContext dbContext, OutboxEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Operation is OutboxOperation.Delete)
        {
            if (entry.ServerId is not { } serverId)
            {
                return SendResult.Abandoned;
            }

            await _calendarClient.DeleteAsync(serverId, cancellationToken);
            return SendResult.Sent;
        }

        var calendarEvent = await dbContext.CalendarEvents.FirstOrDefaultAsync(
            candidate => candidate.LocalId == entry.LocalId, cancellationToken);

        if (calendarEvent is null)
        {
            return SendResult.Abandoned;
        }

        return entry.Operation is OutboxOperation.Create
            ? await SendCreateAsync(calendarEvent, cancellationToken)
            : await SendUpdateAsync(calendarEvent, cancellationToken);
    }

    private async Task<SendResult> SendCreateAsync(LocalCalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        if (calendarEvent.ServerId is not null)
        {
            // Already created - a duplicate create would make a second event out of one.
            return SendResult.Abandoned;
        }

        calendarEvent.ServerId = await _calendarClient.CreateAsync(
            new CreateCalendarEventRequest(ToRequest(calendarEvent.Details)), cancellationToken);
        calendarEvent.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<SendResult> SendUpdateAsync(LocalCalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        if (calendarEvent.ServerId is not { } serverId)
        {
            // Its create is still queued ahead of this and has not succeeded yet.
            return SendResult.Abandoned;
        }

        var outcome = await _calendarClient.UpdateAsync(
            serverId, new UpdateCalendarEventRequest(ToRequest(calendarEvent.Details)), cancellationToken);

        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused an offline edit of event {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Refused;
        }

        calendarEvent.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<(int Received, int RemovedLocally)> PullChangesAsync(
        OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var cursor = await SyncCursors.ReadAsync(dbContext, SyncEntityType.CalendarEvent, cancellationToken);
        var feed = await _calendarClient.GetChangesAsync(cursor, cancellationToken);

        // An event with changes still queued is the one thing the server's version must not overwrite.
        var stillQueued = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.CalendarEvent)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var received = 0;
        foreach (var incoming in feed.Changed)
        {
            var existing = await dbContext.CalendarEvents.FirstOrDefaultAsync(
                calendarEvent => calendarEvent.ServerId == incoming.Id, cancellationToken);

            if (existing is not null && stillQueued.Contains(existing.LocalId))
            {
                continue;
            }

            CopyInto(existing ?? NewLocalEvent(dbContext, incoming.Id), incoming);
            received++;
        }

        var removed = 0;
        foreach (var deletedId in feed.DeletedIds)
        {
            var calendarEvent = await dbContext.CalendarEvents.FirstOrDefaultAsync(
                candidate => candidate.ServerId == deletedId, cancellationToken);

            if (calendarEvent is null || stillQueued.Contains(calendarEvent.LocalId))
            {
                continue;
            }

            dbContext.CalendarEvents.Remove(calendarEvent);
            removed++;
        }

        await SyncCursors.WriteAsync(dbContext, SyncEntityType.CalendarEvent, feed.Cursor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (received, removed);
    }

    private static LocalCalendarEvent NewLocalEvent(OrbitLocalDbContext dbContext, Guid serverId)
    {
        var calendarEvent = new LocalCalendarEvent { LocalId = Guid.NewGuid(), ServerId = serverId };
        dbContext.CalendarEvents.Add(calendarEvent);
        return calendarEvent;
    }

    private void CopyInto(LocalCalendarEvent calendarEvent, CalendarEventDto incoming)
    {
        calendarEvent.Details = incoming.Details;
        calendarEvent.CreatedAtUtc = incoming.CreatedAtUtc;
        calendarEvent.UpdatedAtUtc = incoming.UpdatedAtUtc;
        calendarEvent.IsShared = incoming.IsShared;
        calendarEvent.SharedByUserName = incoming.SharedByUserName;
        calendarEvent.IsSharedWithOthers = incoming.IsSharedWithOthers;
        calendarEvent.AccessLevel = incoming.AccessLevel;
        calendarEvent.OwnerUserId = incoming.OriginalOwnerUserId;
        calendarEvent.LastSyncedAtUtc = _timeProvider.GetUtcNow();
    }

    /// <summary>
    /// The details as the server takes them back. Identical field for field - the two shapes exist
    /// because a request has no server-assigned values in it, not because they disagree.
    ///
    /// Times are forced to UTC on the way out. The store normalises scalar timestamp columns already,
    /// but these travel inside a JSON block and keep whatever offset they were written with - and
    /// Npgsql refuses a non-zero offset for a "timestamp with time zone" column outright, with a 500
    /// that looks nothing like a client mistake. Orbit.Web hit the same wall in its own editors.
    /// </summary>
    private static CalendarEventDetailsRequest ToRequest(CalendarEventDetailsDto details)
        => new(
            details.Title, details.Description,
            details.Location is { } location ? new EventLocationRequest(location.Address, location.Latitude, location.Longitude) : null,
            details.Color, details.StartUtc.ToUniversalTime(), details.EndUtc.ToUniversalTime(), details.IsAllDay,
            details.Recurrence is { } recurrence
                ? new RecurrenceRequest(
                    recurrence.Frequency, recurrence.IntervalCount, recurrence.UntilUtc?.ToUniversalTime(),
                    // The second way a rule can stop. Left out, a repeat set to end after five times in
                    // a browser became one that never ends, the first time the phone saved it.
                    recurrence.OccurrenceCount)
                : null,
            details.Guests, details.ReminderMinutesBeforeStart,
            details.ReminderNotificationChannel,
            // Carried rather than left to the contract's default, which is "Normal": a save writes the
            // whole event, so an event marked High in a browser came back Normal the first time anybody
            // touched it from a phone. The same mistake notes had - see NoteSynchronizer.
            details.Priority,
            // And the same again for what an event says as it begins.
            details.NotifyAtStart);
}

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

        return entry.Operation switch
        {
            OutboxOperation.Create => await SendCreateAsync(dbContext, calendarEvent, cancellationToken),
            OutboxOperation.File => await SendFilingAsync(dbContext, calendarEvent, cancellationToken),
            _ => await SendUpdateAsync(calendarEvent, cancellationToken)
        };
    }

    /// <summary>
    /// Which folder the server should be told, for something filed into one of this phone's. Null both
    /// for one in no folder and for one whose folder has since gone - and a folder the server has not
    /// been told about yet stops the send instead, so the filing waits for it rather than being lost.
    /// The same rule NoteSynchronizer follows, the folders being pushed ahead of everything filed.
    /// </summary>
    private static async Task<Guid?> ServerFolderIdAsync(
        OrbitLocalDbContext dbContext, Guid? folderLocalId, CancellationToken cancellationToken)
    {
        if (folderLocalId is not { } localId)
        {
            return null;
        }

        var folder = await dbContext.Folders.FirstOrDefaultAsync(
            candidate => candidate.LocalId == localId, cancellationToken);

        return folder is null ? null : folder.ServerId ?? throw new FolderNotOnTheServerYet(localId);
    }

    /// <summary>
    /// Puts the event in its folder, or takes it out of one. Its own request to its own endpoint - see
    /// CalendarClient.FileAsync, and MoveToFolderRequest, which says why a save must not carry this.
    /// </summary>
    private async Task<SendResult> SendFilingAsync(
        OrbitLocalDbContext dbContext, LocalCalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        if (calendarEvent.ServerId is not { } serverId)
        {
            // Its create is still queued ahead of this, and that create carries the folder itself.
            return SendResult.Abandoned;
        }

        var outcome = await _calendarClient.FileAsync(
            serverId, await ServerFolderIdAsync(dbContext, calendarEvent.FolderId, cancellationToken), cancellationToken);

        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused a filing of event {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Refused;
        }

        calendarEvent.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<SendResult> SendCreateAsync(
        OrbitLocalDbContext dbContext, LocalCalendarEvent calendarEvent, CancellationToken cancellationToken)
    {
        if (calendarEvent.ServerId is not null)
        {
            // Already created - a duplicate create would make a second event out of one.
            return SendResult.Abandoned;
        }

        var created = await _calendarClient.CreateAsync(
            // The folder travels on the create and only on the create: an event the server has never
            // seen has nothing to file, so LocalCalendarEventRepository queues no filing for one.
            new CreateCalendarEventRequest(
                ToRequest(calendarEvent.Details),
                await ServerFolderIdAsync(dbContext, calendarEvent.FolderId, cancellationToken)),
            cancellationToken);

        if (created is not { Outcome: WriteOutcome.Applied, ServerId: { } serverId })
        {
            // Nothing more to try: the server has answered about this event and will answer the same
            // way tomorrow. Dropped and said out loud rather than queued for ever - see OutboxReplay.
            _logger.LogInformation(
                "The server refused a queued event: {Outcome}", created.Outcome);

            return SendResult.Refused;
        }

        calendarEvent.ServerId = serverId;
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

        // A folder is known here by an id of this phone's own, and arrives named by the server's - see
        // LocalFolder.LocalId. Read once for the run rather than per event.
        var foldersByServerId = await dbContext.Folders
            .Where(folder => folder.ServerId != null)
            .ToDictionaryAsync(folder => folder.ServerId!.Value, folder => folder.LocalId, cancellationToken);

        var received = 0;
        foreach (var incoming in feed.Changed)
        {
            var existing = await dbContext.CalendarEvents.FirstOrDefaultAsync(
                calendarEvent => calendarEvent.ServerId == incoming.Id, cancellationToken);

            if (existing is not null && stillQueued.Contains(existing.LocalId))
            {
                continue;
            }

            CopyInto(existing ?? NewLocalEvent(dbContext, incoming.Id), incoming, foldersByServerId);
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

    private void CopyInto(
        LocalCalendarEvent calendarEvent, CalendarEventDto incoming, IReadOnlyDictionary<Guid, Guid> foldersByServerId)
    {
        // A folder this phone has not heard of leaves the event unfiled rather than pointing at nothing,
        // which is the answer FolderPlacement gives for an id it does not know - see NoteSynchronizer.
        calendarEvent.FolderId = incoming.FolderId is { } folderServerId
            && foldersByServerId.TryGetValue(folderServerId, out var folderLocalId)
                ? folderLocalId
                : null;
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

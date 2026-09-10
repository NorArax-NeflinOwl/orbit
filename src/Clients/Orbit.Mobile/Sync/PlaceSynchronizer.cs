using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;
using Orbit.Core.Sync;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Brings places and the server back into step: send what was done offline, then take what changed
/// elsewhere. The same shape as <see cref="NoteSynchronizer"/>, which says why the order is that way
/// round and why the parts that are not about one feature live in OutboxReplay, SyncFailure and
/// SyncCursors rather than being copied per feature.
///
/// A place handed over reaches this phone through the same feed as one the reader kept: the server's own
/// read of places answers with both (see PlaceAccessResolver), so nothing here has to know the
/// difference - what it copies down says which it is.
/// </summary>
public sealed class PlaceSynchronizer
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly PlacesClient _placesClient;
    private readonly TimeProvider _timeProvider;
    private readonly SyncGate _syncGate;
    private readonly ILogger<PlaceSynchronizer> _logger;

    public PlaceSynchronizer(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, PlacesClient placesClient,
        TimeProvider timeProvider, SyncGate syncGate, ILogger<PlaceSynchronizer> logger)
    {
        _dbContextFactory = dbContextFactory;
        _placesClient = placesClient;
        _timeProvider = timeProvider;
        _syncGate = syncGate;
        _logger = logger;
    }

    /// <inheritdoc cref="NoteSynchronizer.SynchroniseAsync"/>
    public Task<SyncResult> SynchroniseAsync(CancellationToken cancellationToken = default)
        => _syncGate.RunAsync(SyncEntityType.Place, () => RunAsync(cancellationToken), cancellationToken);

    private async Task<SyncResult> RunAsync(CancellationToken cancellationToken)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var push = await OutboxReplay.RunAsync(
            dbContext, SyncEntityType.Place,
            (entry, token) => SendAsync(dbContext, entry, token), _timeProvider, _logger, cancellationToken);

        try
        {
            var pull = await PullChangesAsync(dbContext, cancellationToken);
            return new SyncResult(push.Sent, pull.Received, pull.RemovedLocally, push.GivenUp, ReachedTheServer: true);
        }
        catch (Exception exception) when (SyncFailure.IsWorthRetrying(exception, cancellationToken))
        {
            _logger.LogInformation("Could not reach the server to pull places ({Reason})", exception.Message);
            return push.Sent > 0
                ? new SyncResult(push.Sent, 0, 0, push.GivenUp, ReachedTheServer: true)
                : SyncResult.NeverGotThrough(push.GivenUp);
        }
    }

    private async Task<SendResult> SendAsync(
        OrbitLocalDbContext dbContext, OutboxEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Operation is OutboxOperation.Delete)
        {
            if (entry.ServerId is not { } serverId)
            {
                return SendResult.Abandoned;
            }

            await _placesClient.DeleteAsync(serverId, cancellationToken);
            return SendResult.Sent;
        }

        var place = await dbContext.Places.FirstOrDefaultAsync(
            candidate => candidate.LocalId == entry.LocalId, cancellationToken);

        if (place is null)
        {
            // Deleted locally before this ever went out. LocalPlaceRepository drops the queue for a
            // place the server never saw, so reaching here means the row went away another way.
            return SendResult.Abandoned;
        }

        return entry.Operation is OutboxOperation.Create
            ? await SendCreateAsync(place, cancellationToken)
            : await SendUpdateAsync(place, cancellationToken);
    }

    private async Task<SendResult> SendCreateAsync(LocalPlace place, CancellationToken cancellationToken)
    {
        if (place.ServerId is not null)
        {
            // Already created - a duplicate create would make a second place out of one.
            return SendResult.Abandoned;
        }

        place.ServerId = await _placesClient.CreateAsync(Saving(place), cancellationToken);
        place.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    private async Task<SendResult> SendUpdateAsync(LocalPlace place, CancellationToken cancellationToken)
    {
        if (place.ServerId is not { } serverId)
        {
            // Its create is still queued ahead of this and has not succeeded yet.
            return SendResult.Abandoned;
        }

        var outcome = await _placesClient.UpdateAsync(serverId, Saving(place), cancellationToken);
        if (outcome is not WriteOutcome.Applied)
        {
            _logger.LogInformation("The server refused an offline edit of place {ServerId}: {Outcome}", serverId, outcome);
            return SendResult.Refused;
        }

        place.LastSyncedAtUtc = _timeProvider.GetUtcNow();
        return SendResult.Sent;
    }

    /// <summary>
    /// One request for making a place and for changing one, because a place says exactly the same thing
    /// either way - see SavePlaceRequest, where the server states the same.
    /// </summary>
    private static SavePlaceRequest Saving(LocalPlace place)
        => new(
            place.Name,
            new EventLocationDto(place.Address, place.Latitude, place.Longitude),
            place.Description,
            place.Colour,
            place.Priority,
            place.TaskListIds);

    private async Task<(int Received, int RemovedLocally)> PullChangesAsync(
        OrbitLocalDbContext dbContext, CancellationToken cancellationToken)
    {
        var cursor = await SyncCursors.ReadAsync(dbContext, SyncEntityType.Place, cancellationToken);
        var feed = await _placesClient.GetChangesAsync(cursor, cancellationToken);

        // A place with changes still queued is the one thing the server's version must not overwrite -
        // the reader's unsent work would disappear with no trace that it existed.
        var stillQueued = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.Place)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var received = 0;
        foreach (var incoming in feed.Changed)
        {
            var existing = await dbContext.Places.FirstOrDefaultAsync(
                place => place.ServerId == incoming.Id, cancellationToken);

            if (existing is not null && stillQueued.Contains(existing.LocalId))
            {
                continue;
            }

            CopyInto(existing ?? NewLocalPlace(dbContext, incoming.Id), incoming);
            received++;
        }

        var removed = 0;
        foreach (var deletedId in feed.DeletedIds)
        {
            var place = await dbContext.Places.FirstOrDefaultAsync(
                candidate => candidate.ServerId == deletedId, cancellationToken);

            if (place is null || stillQueued.Contains(place.LocalId))
            {
                continue;
            }

            dbContext.Places.Remove(place);
            removed++;
        }

        await SyncCursors.WriteAsync(dbContext, SyncEntityType.Place, feed.Cursor, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return (received, removed);
    }

    private static LocalPlace NewLocalPlace(OrbitLocalDbContext dbContext, Guid serverId)
    {
        var place = new LocalPlace { LocalId = Guid.NewGuid(), ServerId = serverId };
        dbContext.Places.Add(place);
        return place;
    }

    private void CopyInto(LocalPlace place, PlaceDto incoming)
    {
        place.Name = incoming.Name;
        place.Description = incoming.Description;
        place.Address = incoming.Where.Address ?? string.Empty;
        place.Latitude = incoming.Where.Latitude;
        place.Longitude = incoming.Where.Longitude;
        place.Colour = incoming.Colour;
        place.Priority = incoming.Priority;
        place.TaskListIds = incoming.TaskListIds;
        place.CreatedAtUtc = incoming.CreatedAtUtc;
        place.UpdatedAtUtc = incoming.UpdatedAtUtc;
        place.IsShared = incoming.IsShared;
        place.SharedByUserName = incoming.SharedByUserName;
        place.IsSharedWithOthers = incoming.IsSharedWithOthers;
        place.AccessLevel = incoming.AccessLevel;
        place.OwnerUserId = incoming.OriginalOwnerUserId;
        place.LastSyncedAtUtc = _timeProvider.GetUtcNow();
    }
}

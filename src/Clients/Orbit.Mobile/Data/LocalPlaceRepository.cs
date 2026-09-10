using Microsoft.EntityFrameworkCore;
using Orbit.Core.Sync;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// A place as the reader has just left it. The same shape <see cref="NoteContent"/> and
/// <see cref="InventoryContent"/> take, so a change made to one repository is obvious in the others.
/// </summary>
public sealed record PlaceContent(
    string Name,
    string Description,
    string Address,
    double Latitude,
    double Longitude,
    string Colour = "",
    string Priority = "Normal",
    IReadOnlyList<Guid>? TaskListIds = null);

/// <summary>
/// Every read and write a screen performs on places, with each write recording its own outbox entry in
/// the same transaction as the change - the rule all four of the other repositories follow.
///
/// Simpler than they are by exactly what a place does not have: nothing to unseal, and no
/// copy-for-editing. What it keeps is the two refusals that matter - a place handed over to read is not
/// writable at all, and one somebody else may be changing is not writable while this phone is offline.
/// </summary>
public sealed class LocalPlaceRepository
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;

    public LocalPlaceRepository(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider,
        INetworkStatus networkStatus)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _networkStatus = networkStatus;
    }

    /// <summary>Most recently changed first, which is the order every list on this phone reads in.</summary>
    public async Task<IReadOnlyList<LocalPlace>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Places
            .AsNoTracking()
            .OrderByDescending(place => place.UpdatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<LocalPlace?> FindAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        return await dbContext.Places
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);
    }

    /// <summary>Whether this place may be changed right now, without changing it.</summary>
    public async Task<bool> CanEditAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var place = await dbContext.Places.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);

        return place is not null
            && SharedItemAccess.AllowsEditing(place)
            && OfflineEditPolicy.IsAllowed(place, _networkStatus);
    }

    /// <summary>Which places are still waiting to reach the server - what a screen draws as "not sent yet".</summary>
    public async Task<IReadOnlySet<Guid>> GetPendingLocalIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var localIds = await dbContext.Outbox
            .Where(entry => entry.EntityType == SyncEntityType.Place)
            .Select(entry => entry.LocalId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return localIds.ToHashSet();
    }

    public async Task<LocalPlace> CreateAsync(PlaceContent content, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var now = _timeProvider.GetUtcNow();
        var place = new LocalPlace
        {
            LocalId = Guid.NewGuid(),
            ServerId = null,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        Write(place, content);
        dbContext.Places.Add(place);
        Enqueue(dbContext, place.LocalId, OutboxOperation.Create, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return place;
    }

    public async Task<LocalWriteOutcome> UpdateAsync(
        Guid localId, PlaceContent content, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Places.FirstOrDefaultAsync(
                candidate => candidate.LocalId == localId, cancellationToken) is not { } place)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!SharedItemAccess.AllowsEditing(place))
        {
            return LocalWriteOutcome.RefusedAsReadOnly;
        }

        if (!OfflineEditPolicy.IsAllowed(place, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        var now = _timeProvider.GetUtcNow();
        Write(place, content);
        place.UpdatedAtUtc = now;
        Enqueue(dbContext, localId, OutboxOperation.Update, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    /// <summary>
    /// Forgets it. For a place handed over this is the server's own answer to a recipient's delete: the
    /// grant goes and the owner's place stays - see DeletePlaceCommandHandler, which decides that. The
    /// phone sends the same request either way and does not have to know which of the two it was.
    /// </summary>
    public async Task<LocalWriteOutcome> DeleteAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (await dbContext.Places.FirstOrDefaultAsync(
                candidate => candidate.LocalId == localId, cancellationToken) is not { } place)
        {
            return LocalWriteOutcome.NotFound;
        }

        if (!OfflineEditPolicy.IsAllowed(place, _networkStatus))
        {
            return LocalWriteOutcome.RefusedWhileOffline;
        }

        dbContext.Places.Remove(place);

        if (place.ServerId is null)
        {
            // Never reached the server, so there is nothing out there to delete - and the create still
            // in the queue must go with it, or replay would make the place it was asked to forget.
            dbContext.Outbox.RemoveRange(dbContext.Outbox.Where(
                entry => entry.EntityType == SyncEntityType.Place && entry.LocalId == localId));
        }
        else
        {
            Enqueue(dbContext, localId, OutboxOperation.Delete, _timeProvider.GetUtcNow(), place.ServerId);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return LocalWriteOutcome.Applied;
    }

    private static void Write(LocalPlace place, PlaceContent content)
    {
        place.Name = content.Name.Trim();
        place.Description = content.Description.Trim();
        place.Address = content.Address.Trim();
        place.Latitude = content.Latitude;
        place.Longitude = content.Longitude;
        place.Colour = content.Colour.Trim();
        place.Priority = content.Priority;
        place.TaskListIds = content.TaskListIds ?? [];
    }

    private static void Enqueue(
        OrbitLocalDbContext dbContext, Guid localId, OutboxOperation operation, DateTimeOffset queuedAtUtc,
        Guid? serverId = null)
        => dbContext.Outbox.Add(new OutboxEntry
        {
            EntityType = SyncEntityType.Place,
            LocalId = localId,
            ServerId = serverId,
            Operation = operation,
            QueuedAtUtc = queuedAtUtc
        });
}

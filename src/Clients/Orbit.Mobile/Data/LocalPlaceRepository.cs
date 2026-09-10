using Microsoft.EntityFrameworkCore;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Places;
using Orbit.Core.Sync;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Data;

/// <summary>
/// A place as the reader has just left it. The same shape <see cref="NoteContent"/> and
/// <see cref="InventoryContent"/> take, so a change made to one repository is obvious in the others.
/// </summary>
/// <param name="IsPrivate">
/// Sealed unless the reader says otherwise, which is the opposite default from every other kind of thing
/// on this phone - see Orbit.Core.Places.Place.IsPrivate.
/// </param>
public sealed record PlaceContent(
    string Name,
    string Description,
    string Address,
    double Latitude,
    double Longitude,
    string Colour = "",
    string Priority = "Normal",
    IReadOnlyList<Guid>? TaskListIds = null,
    bool IsPrivate = true);

/// <summary>
/// Every read and write a screen performs on places, with each write recording its own outbox entry in
/// the same transaction as the change - the rule all four of the other repositories follow.
///
/// Simpler than they are by what a place does not have - there is no copy-for-editing - and busier by
/// what it does: a place is sealed unless its owner said otherwise, so nearly every read here has to open
/// one and nearly every write has to seal one. What it keeps from the others is the two refusals that
/// matter: a place handed over to read is not writable at all, and one somebody else may be changing is
/// not writable while this phone is offline.
/// </summary>
public sealed class LocalPlaceRepository
{
    private readonly IDbContextFactory<OrbitLocalDbContext> _dbContextFactory;
    private readonly TimeProvider _timeProvider;
    private readonly INetworkStatus _networkStatus;
    private readonly PrivateContentSealer _privateContent;

    public LocalPlaceRepository(
        IDbContextFactory<OrbitLocalDbContext> dbContextFactory, TimeProvider timeProvider,
        INetworkStatus networkStatus, PrivateContentSealer privateContent)
    {
        _dbContextFactory = dbContextFactory;
        _timeProvider = timeProvider;
        _networkStatus = networkStatus;
        _privateContent = privateContent;
    }

    /// <summary>Most recently changed first, which is the order every list on this phone reads in.</summary>
    public async Task<IReadOnlyList<LocalPlace>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var places = await dbContext.Places
            .AsNoTracking()
            .OrderByDescending(place => place.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        await OpenSealedPlacesAsync(places, cancellationToken);
        return places;
    }

    public async Task<LocalPlace?> FindAsync(Guid localId, CancellationToken cancellationToken = default)
    {
        await using var dbContext = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var place = await dbContext.Places
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.LocalId == localId, cancellationToken);

        if (place is not null)
        {
            await OpenSealedPlacesAsync([place], cancellationToken);
        }

        return place;
    }

    /// <inheritdoc cref="LocalNoteRepository.OpenPrivateContentAsync"/>
    private async Task OpenSealedPlacesAsync(IReadOnlyList<LocalPlace> places, CancellationToken cancellationToken)
    {
        var sealedPlaces = places.Where(place => place.IsPrivate).ToList();
        if (sealedPlaces.Count == 0)
        {
            return;
        }

        PrivateContentKey key;
        try
        {
            key = await _privateContent.UnlockAsync(cancellationToken);
        }
        catch (EncryptionKeyLockedException)
        {
            foreach (var place in sealedPlaces)
            {
                place.IsSealed = true;
            }

            return;
        }

        using (key)
        {
            foreach (var place in sealedPlaces)
            {
                Open(key, place);
            }
        }
    }

    private static void Open(PrivateContentKey key, LocalPlace place)
    {
        if (place.EncryptedContent is not { } sealedContent
            || key.Open(sealedContent, SealedContentSerializerContext.Default.SealedPlace) is not { } opened)
        {
            place.IsSealed = true;
            return;
        }

        place.Name = opened.Name;
        place.Description = opened.Description;
        place.Address = opened.Where.Address ?? string.Empty;
        place.Latitude = opened.Where.Latitude;
        place.Longitude = opened.Where.Longitude;
        place.IsSealed = false;
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

        await WriteAsync(place, content, cancellationToken);
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
        await WriteAsync(place, content, cancellationToken);
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

    /// <inheritdoc cref="LocalNoteRepository.WriteContentAsync"/>
    private async Task WriteAsync(LocalPlace place, PlaceContent content, CancellationToken cancellationToken)
    {
        place.Colour = content.Colour.Trim();
        place.Priority = content.Priority;
        place.TaskListIds = content.TaskListIds ?? [];
        place.IsPrivate = content.IsPrivate;
        place.IsSealed = false;

        if (!content.IsPrivate)
        {
            place.Name = content.Name.Trim();
            place.Description = content.Description.Trim();
            place.Address = content.Address.Trim();
            place.Latitude = content.Latitude;
            place.Longitude = content.Longitude;
            place.EncryptedCiphertext = null;
            place.EncryptedNonce = null;
            return;
        }

        using var key = await _privateContent.UnlockAsync(cancellationToken);
        var sealedContent = key.Seal(
            new SealedPlace(
                content.Name.Trim(), content.Description.Trim(),
                new EventLocationDto(content.Address.Trim(), content.Latitude, content.Longitude)),
            SealedContentSerializerContext.Default.SealedPlace);

        // Emptied rather than merely unread, the point with the words: a place whose coordinates were
        // still on this phone in the clear would be sealed in name only.
        place.Name = string.Empty;
        place.Description = string.Empty;
        place.Address = string.Empty;
        place.Latitude = 0;
        place.Longitude = 0;
        place.EncryptedCiphertext = sealedContent.Ciphertext;
        place.EncryptedNonce = sealedContent.Nonce;
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

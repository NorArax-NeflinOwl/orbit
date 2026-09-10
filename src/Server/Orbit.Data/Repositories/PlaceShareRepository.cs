using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Places;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

/// <summary>Mirrors NoteShareRepository - see PlaceShare on why a place's grants are the simpler half of it.</summary>
public sealed class PlaceShareRepository : IPlaceShareRepository
{
    private readonly OrbitDbContext _dbContext;

    public PlaceShareRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(PlaceShare share, CancellationToken cancellationToken)
    {
        _dbContext.PlaceShares.Add(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlaceShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PlaceShares
            .AsNoTracking()
            .FirstOrDefaultAsync(share => share.Id == id && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(PlaceShare share, CancellationToken cancellationToken)
    {
        _dbContext.PlaceShares.Update(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PlaceShare?> FindExistingAsync(Guid sourcePlaceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PlaceShares
            .AsNoTracking()
            .FirstOrDefaultAsync(
                share => share.SourcePlaceId == sourcePlaceId && share.RecipientUserId == recipientUserId,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<PlaceShare?> FindAcceptedGrantAsync(Guid sourcePlaceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PlaceShares
            .AsNoTracking()
            .FirstOrDefaultAsync(
                share => share.SourcePlaceId == sourcePlaceId
                    && share.RecipientUserId == recipientUserId
                    && share.AcceptedAtUtc != null,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<PlaceShare>> GetAcceptedGrantsForRecipientAsync(
        Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.PlaceShares
            .AsNoTracking()
            .Where(share => share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null)
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToDomain)];
    }

    public async Task<IReadOnlySet<Guid>> GetSharedOutPlaceIdsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var placeIds = await _dbContext.PlaceShares
            .AsNoTracking()
            .Where(share => share.OwnerUserId == ownerUserId && share.AcceptedAtUtc != null)
            .Select(share => share.SourcePlaceId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return placeIds.ToHashSet();
    }

    public async Task RemoveAcceptedGrantAsync(Guid sourcePlaceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        await _dbContext.PlaceShares
            .Where(share => share.SourcePlaceId == sourcePlaceId
                && share.RecipientUserId == recipientUserId
                && share.AcceptedAtUtc != null)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PlaceShare>> GetSharesToAsync(
        Guid ownerUserId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.PlaceShares
            .AsNoTracking()
            .Where(share => share.OwnerUserId == ownerUserId && share.RecipientUserId == recipientUserId)
            .OrderBy(share => share.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return [.. entities.Select(ToDomain)];
    }

    public async Task<bool> RemoveAsync(Guid ownerUserId, Guid shareId, CancellationToken cancellationToken)
    {
        var removed = await _dbContext.PlaceShares
            .Where(share => share.Id == shareId && share.OwnerUserId == ownerUserId)
            .ExecuteDeleteAsync(cancellationToken);

        return removed > 0;
    }

    private static PlaceShare ToDomain(PlaceShareEntity entity)
        => PlaceShare.FromPersistence(
            entity.Id, entity.SourcePlaceId, entity.OwnerUserId, entity.RecipientUserId,
            Enum.Parse<ShareAccessLevel>(entity.AccessLevel), entity.CreatedAtUtc, entity.AcceptedAtUtc);

    private static PlaceShareEntity ToEntity(PlaceShare share)
        => new()
        {
            Id = share.Id,
            SourcePlaceId = share.SourcePlaceId,
            OwnerUserId = share.OwnerUserId,
            RecipientUserId = share.RecipientUserId,
            AccessLevel = share.AccessLevel.ToString(),
            CreatedAtUtc = share.CreatedAtUtc,
            AcceptedAtUtc = share.AcceptedAtUtc
        };
}

using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class InventoryShareRepository : IInventoryShareRepository
{
    private readonly OrbitDbContext _dbContext;

    public InventoryShareRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(InventoryShare share, CancellationToken cancellationToken)
    {
        _dbContext.InventoryShares.Add(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InventoryShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryShares
            .AsNoTracking()
            .FirstOrDefaultAsync(share => share.Id == id && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(InventoryShare share, CancellationToken cancellationToken)
    {
        _dbContext.InventoryShares.Update(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<InventoryShare?> FindExistingAsync(Guid sourceInventoryId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryShares
            .AsNoTracking()
            .FirstOrDefaultAsync(
                share => share.SourceInventoryId == sourceInventoryId && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<InventoryShare?> FindAcceptedGrantAsync(Guid sourceInventoryId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryShares
            .AsNoTracking()
            .FirstOrDefaultAsync(
                share => share.SourceInventoryId == sourceInventoryId && share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<InventoryShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.InventoryShares
            .AsNoTracking()
            .Where(share => share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlySet<Guid>> GetSharedOutInventoryIdsAsync(Guid ownerUserId, CancellationToken cancellationToken)
    {
        var ids = await _dbContext.InventoryShares
            .AsNoTracking()
            .Where(share => share.OwnerUserId == ownerUserId && share.AcceptedAtUtc != null)
            .Select(share => share.SourceInventoryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    private static InventoryShare ToDomain(InventoryShareEntity entity)
        => InventoryShare.FromPersistence(
            entity.Id, entity.SourceInventoryId, entity.OwnerUserId, entity.RecipientUserId,
            Enum.Parse<ShareAccessLevel>(entity.AccessLevel), entity.CreatedAtUtc, entity.AcceptedAtUtc);

    private static InventoryShareEntity ToEntity(InventoryShare share)
        => new()
        {
            Id = share.Id,
            SourceInventoryId = share.SourceInventoryId,
            OwnerUserId = share.OwnerUserId,
            RecipientUserId = share.RecipientUserId,
            AccessLevel = share.AccessLevel.ToString(),
            CreatedAtUtc = share.CreatedAtUtc,
            AcceptedAtUtc = share.AcceptedAtUtc
        };
    public async Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        await _dbContext.InventoryShares
            .Where(share => share.SourceInventoryId == sourceId && share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
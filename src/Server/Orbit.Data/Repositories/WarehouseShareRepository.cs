using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class WarehouseShareRepository : IWarehouseShareRepository
{
    private readonly OrbitDbContext _dbContext;

    public WarehouseShareRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(WarehouseShare share, CancellationToken cancellationToken)
    {
        _dbContext.WarehouseShares.Add(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WarehouseShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.WarehouseShares
            .AsNoTracking()
            .FirstOrDefaultAsync(share => share.Id == id && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(WarehouseShare share, CancellationToken cancellationToken)
    {
        _dbContext.WarehouseShares.Update(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<WarehouseShare?> FindExistingAsync(Guid sourceWarehouseId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.WarehouseShares
            .AsNoTracking()
            .FirstOrDefaultAsync(
                share => share.SourceWarehouseId == sourceWarehouseId && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<WarehouseShare?> FindAcceptedGrantAsync(Guid sourceWarehouseId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.WarehouseShares
            .AsNoTracking()
            .FirstOrDefaultAsync(
                share => share.SourceWarehouseId == sourceWarehouseId && share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<WarehouseShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.WarehouseShares
            .AsNoTracking()
            .Where(share => share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    private static WarehouseShare ToDomain(WarehouseShareEntity entity)
        => WarehouseShare.FromPersistence(
            entity.Id, entity.SourceWarehouseId, entity.OwnerUserId, entity.RecipientUserId,
            Enum.Parse<ShareAccessLevel>(entity.AccessLevel), entity.CreatedAtUtc, entity.AcceptedAtUtc);

    private static WarehouseShareEntity ToEntity(WarehouseShare share)
        => new()
        {
            Id = share.Id,
            SourceWarehouseId = share.SourceWarehouseId,
            OwnerUserId = share.OwnerUserId,
            RecipientUserId = share.RecipientUserId,
            AccessLevel = share.AccessLevel.ToString(),
            CreatedAtUtc = share.CreatedAtUtc,
            AcceptedAtUtc = share.AcceptedAtUtc
        };
    public async Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        await _dbContext.WarehouseShares
            .Where(share => share.SourceWarehouseId == sourceId && share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
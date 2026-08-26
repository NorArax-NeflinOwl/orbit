using Microsoft.EntityFrameworkCore;
using Orbit.Core.Sync;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class SyncTombstoneRepository : ISyncTombstoneRepository
{
    private readonly OrbitDbContext _dbContext;

    public SyncTombstoneRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task RecordAsync(SyncTombstone tombstone, CancellationToken cancellationToken)
    {
        _dbContext.SyncTombstones.Add(new SyncTombstoneEntity
        {
            Id = Guid.NewGuid(),
            UserId = tombstone.UserId,
            EntityType = tombstone.EntityType,
            EntityId = tombstone.EntityId,
            DeletedAtUtc = tombstone.DeletedAtUtc
        });

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>> GetDeletedIdsSinceAsync(
        Guid userId, string entityType, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
        => await _dbContext.SyncTombstones
            .AsNoTracking()
            .Where(tombstone => tombstone.UserId == userId
                && tombstone.EntityType == entityType
                && tombstone.DeletedAtUtc >= sinceUtc)
            .Select(tombstone => tombstone.EntityId)
            .ToListAsync(cancellationToken);
}

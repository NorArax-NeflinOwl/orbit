using Microsoft.EntityFrameworkCore;
using Orbit.Core.Location;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class SharedLocationRepository : ISharedLocationRepository
{
    private readonly OrbitDbContext _dbContext;

    public SharedLocationRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SharedLocation?> FindAsync(Guid sharerUserId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.SharedLocations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                shared => shared.SharerUserId == sharerUserId && shared.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(SharedLocation sharedLocation, CancellationToken cancellationToken)
    {
        _dbContext.SharedLocations.Add(ToEntity(sharedLocation));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SharedLocation sharedLocation, CancellationToken cancellationToken)
    {
        _dbContext.SharedLocations.Update(ToEntity(sharedLocation));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<SharedLocation>> GetSharedWithAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.SharedLocations
            .AsNoTracking()
            .Where(shared => shared.RecipientUserId == recipientUserId)
            .OrderByDescending(shared => shared.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<SharedLocation>> GetSharedByAsync(Guid sharerUserId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.SharedLocations
            .AsNoTracking()
            .Where(shared => shared.SharerUserId == sharerUserId)
            .OrderByDescending(shared => shared.UpdatedAtUtc)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task DeleteAsync(Guid sharerUserId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        await _dbContext.SharedLocations
            .Where(shared => shared.SharerUserId == sharerUserId && shared.RecipientUserId == recipientUserId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    public async Task DeleteAllBySharerAsync(Guid sharerUserId, CancellationToken cancellationToken)
    {
        await _dbContext.SharedLocations
            .Where(shared => shared.SharerUserId == sharerUserId)
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static SharedLocation ToDomain(SharedLocationEntity entity)
        => SharedLocation.FromPersistence(
            entity.Id, entity.SharerUserId, entity.RecipientUserId, entity.CiphertextBase64, entity.NonceBase64,
            entity.IsContinuous, entity.UpdatedAtUtc);

    private static SharedLocationEntity ToEntity(SharedLocation sharedLocation)
        => new()
        {
            Id = sharedLocation.Id,
            SharerUserId = sharedLocation.SharerUserId,
            RecipientUserId = sharedLocation.RecipientUserId,
            CiphertextBase64 = sharedLocation.CiphertextBase64,
            NonceBase64 = sharedLocation.NonceBase64,
            IsContinuous = sharedLocation.IsContinuous,
            UpdatedAtUtc = sharedLocation.UpdatedAtUtc
        };
}

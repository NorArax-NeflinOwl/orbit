using Microsoft.EntityFrameworkCore;
using Orbit.Core.Sharing;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class PublicShareLinkRepository : IPublicShareLinkRepository
{
    private readonly OrbitDbContext _dbContext;

    public PublicShareLinkRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(PublicShareLink link, CancellationToken cancellationToken)
    {
        _dbContext.PublicShareLinks.Add(ToEntity(link));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<PublicShareLink?> GetByTokenAsync(string token, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PublicShareLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(link => link.Token == token, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<PublicShareLink?> GetLiveForItemAsync(
        Guid ownerUserId, SharedItemType itemType, Guid itemId, CancellationToken cancellationToken)
    {
        var itemTypeName = itemType.ToString();
        var entity = await _dbContext.PublicShareLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(
                link => link.OwnerUserId == ownerUserId
                    && link.ItemType == itemTypeName
                    && link.ItemId == itemId
                    && link.RevokedAtUtc == null,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(PublicShareLink link, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.PublicShareLinks.FirstOrDefaultAsync(row => row.Id == link.Id, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.RevokedAtUtc = link.RevokedAtUtc;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static PublicShareLink ToDomain(PublicShareLinkEntity entity)
        => PublicShareLink.FromPersistence(
            entity.Id, entity.Token, entity.OwnerUserId, Enum.Parse<SharedItemType>(entity.ItemType, ignoreCase: true),
            entity.ItemId, entity.CreatedAtUtc, entity.RevokedAtUtc);

    private static PublicShareLinkEntity ToEntity(PublicShareLink link)
        => new()
        {
            Id = link.Id,
            Token = link.Token,
            OwnerUserId = link.OwnerUserId,
            ItemType = link.ItemType.ToString(),
            ItemId = link.ItemId,
            CreatedAtUtc = link.CreatedAtUtc,
            RevokedAtUtc = link.RevokedAtUtc
        };
}

using Microsoft.EntityFrameworkCore;
using Orbit.Core.Users;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly OrbitDbContext _dbContext;

    public RefreshTokenRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(refreshToken => refreshToken.TokenHash == tokenHash, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        _dbContext.RefreshTokens.Add(ToEntity(refreshToken));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(RefreshToken refreshToken, CancellationToken cancellationToken)
    {
        _dbContext.RefreshTokens.Update(ToEntity(refreshToken));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static RefreshToken ToDomain(RefreshTokenEntity entity)
        => RefreshToken.FromPersistence(
            entity.Id, entity.UserId, entity.TokenHash, entity.ExpiresAtUtc, entity.CreatedAtUtc, entity.RevokedAtUtc);

    private static RefreshTokenEntity ToEntity(RefreshToken refreshToken)
        => new()
        {
            Id = refreshToken.Id,
            UserId = refreshToken.UserId,
            TokenHash = refreshToken.TokenHash,
            ExpiresAtUtc = refreshToken.ExpiresAtUtc,
            CreatedAtUtc = refreshToken.CreatedAtUtc,
            RevokedAtUtc = refreshToken.RevokedAtUtc
        };
}

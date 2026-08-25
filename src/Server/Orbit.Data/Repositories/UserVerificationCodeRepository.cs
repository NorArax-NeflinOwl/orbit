using Microsoft.EntityFrameworkCore;
using Orbit.Core.Users;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class UserVerificationCodeRepository : IUserVerificationCodeRepository
{
    private readonly OrbitDbContext _dbContext;

    public UserVerificationCodeRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(UserVerificationCode code, CancellationToken cancellationToken)
    {
        _dbContext.UserVerificationCodes.Add(ToEntity(code));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(UserVerificationCode code, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.UserVerificationCodes.FirstAsync(row => row.Id == code.Id, cancellationToken);
        entity.ConsumedAtUtc = code.ConsumedAtUtc;
        entity.FailedAttempts = code.FailedAttempts;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserVerificationCode?> FindActiveAsync(
        Guid userId, VerificationCodePurpose purpose, CancellationToken cancellationToken)
    {
        var purposeValue = purpose.ToString();
        var nowUtc = DateTimeOffset.UtcNow;
        var entity = await _dbContext.UserVerificationCodes
            .AsNoTracking()
            .Where(row => row.UserId == userId
                && row.Purpose == purposeValue
                && row.ConsumedAtUtc == null
                && row.FailedAttempts < UserVerificationCode.MaxFailedAttempts
                && row.ExpiresAtUtc > nowUtc)
            .OrderByDescending(row => row.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task ConsumeAllAsync(Guid userId, VerificationCodePurpose purpose, CancellationToken cancellationToken)
    {
        var purposeValue = purpose.ToString();
        var entities = await _dbContext.UserVerificationCodes
            .Where(row => row.UserId == userId && row.Purpose == purposeValue && row.ConsumedAtUtc == null)
            .ToListAsync(cancellationToken);
        if (entities.Count == 0)
        {
            return;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        foreach (var entity in entities)
        {
            entity.ConsumedAtUtc = nowUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static UserVerificationCode ToDomain(UserVerificationCodeEntity entity)
        => UserVerificationCode.FromPersistence(
            entity.Id, entity.UserId, Enum.Parse<VerificationCodePurpose>(entity.Purpose), entity.CodeHash,
            entity.EmailAddress, entity.ExpiresAtUtc, entity.CreatedAtUtc, entity.ConsumedAtUtc, entity.FailedAttempts);

    private static UserVerificationCodeEntity ToEntity(UserVerificationCode code)
        => new()
        {
            Id = code.Id,
            UserId = code.UserId,
            Purpose = code.Purpose.ToString(),
            CodeHash = code.CodeHash,
            EmailAddress = code.EmailAddress,
            ExpiresAtUtc = code.ExpiresAtUtc,
            CreatedAtUtc = code.CreatedAtUtc,
            ConsumedAtUtc = code.ConsumedAtUtc,
            FailedAttempts = code.FailedAttempts
        };
}

using Microsoft.EntityFrameworkCore;
using Orbit.Core.Users;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly OrbitDbContext _dbContext;

    public UserRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Id == id, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<User?> GetByUserNameAsync(string userName, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(user => user.UserName == userName, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Add(ToEntity(user));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(User user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Update(ToEntity(user));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static User ToDomain(UserEntity entity)
        => User.FromPersistence(
            entity.Id, entity.Email, entity.UserName, entity.DisplayName, entity.PasswordHash, entity.CreatedAtUtc,
            entity.PublicKeyBase64, ToWrappedPrivateKey(entity));

    private static UserEntity ToEntity(User user)
        => new()
        {
            Id = user.Id,
            Email = user.Email,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            PasswordHash = user.PasswordHash,
            CreatedAtUtc = user.CreatedAtUtc,
            PublicKeyBase64 = user.PublicKeyBase64,
            WrappedPrivateKeyBase64 = user.WrappedPrivateKey?.CiphertextBase64,
            PrivateKeyWrapNonceBase64 = user.WrappedPrivateKey?.NonceBase64,
            PrivateKeySaltBase64 = user.WrappedPrivateKey?.SaltBase64,
            PrivateKeyDerivationIterations = user.WrappedPrivateKey?.Iterations
        };

    /// <summary>
    /// The four wrapped-private-key columns are only ever written together (see ToEntity) and read back
    /// together here - null unless every one of them is present, rather than trusting just one to decide
    /// whether a backup exists.
    /// </summary>
    private static WrappedPrivateKey? ToWrappedPrivateKey(UserEntity entity)
    {
        if (entity.WrappedPrivateKeyBase64 is null || entity.PrivateKeyWrapNonceBase64 is null ||
            entity.PrivateKeySaltBase64 is null || entity.PrivateKeyDerivationIterations is null)
        {
            return null;
        }

        return new WrappedPrivateKey(
            entity.WrappedPrivateKeyBase64, entity.PrivateKeyWrapNonceBase64, entity.PrivateKeySaltBase64,
            entity.PrivateKeyDerivationIterations.Value);
    }
}

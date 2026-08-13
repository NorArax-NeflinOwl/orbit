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

    public async Task AddAsync(User user, CancellationToken cancellationToken)
    {
        _dbContext.Users.Add(ToEntity(user));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static User ToDomain(UserEntity entity)
        => User.FromPersistence(entity.Id, entity.Email, entity.DisplayName, entity.PasswordHash, entity.CreatedAtUtc);

    private static UserEntity ToEntity(User user)
        => new()
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            PasswordHash = user.PasswordHash,
            CreatedAtUtc = user.CreatedAtUtc
        };
}

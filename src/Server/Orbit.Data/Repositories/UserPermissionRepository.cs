using Microsoft.EntityFrameworkCore;
using Orbit.Core.Permissions;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class UserPermissionRepository : IUserPermissionRepository
{
    private readonly OrbitDbContext _dbContext;

    public UserPermissionRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// A stored name that no longer parses is dropped rather than thrown over: a permission removed from
    /// the enum should stop granting anything, not make every other permission on the account unreadable.
    /// </summary>
    public async Task<IReadOnlySet<ApplicationPermission>> GetForUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var names = await _dbContext.UserPermissions
            .AsNoTracking()
            .Where(permission => permission.UserId == userId)
            .Select(permission => permission.Permission)
            .ToListAsync(cancellationToken);

        var granted = new HashSet<ApplicationPermission>();
        foreach (var name in names)
        {
            if (Enum.TryParse<ApplicationPermission>(name, out var permission))
            {
                granted.Add(permission);
            }
        }

        return granted;
    }

    public async Task GrantAsync(Guid userId, ApplicationPermission permission, CancellationToken cancellationToken)
    {
        var name = permission.ToString();
        var alreadyGranted = await _dbContext.UserPermissions
            .AnyAsync(existing => existing.UserId == userId && existing.Permission == name, cancellationToken);
        if (alreadyGranted)
        {
            return;
        }

        _dbContext.UserPermissions.Add(new UserPermissionEntity
        {
            UserId = userId,
            Permission = name,
            GrantedAtUtc = DateTimeOffset.UtcNow
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

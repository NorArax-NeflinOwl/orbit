using Microsoft.EntityFrameworkCore;
using Orbit.Core.Permissions;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class PermissionCodeRepository : IPermissionCodeRepository
{
    private readonly OrbitDbContext _dbContext;

    public PermissionCodeRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>
    /// A stored name that no longer parses is skipped rather than thrown over: a permission dropped from
    /// the enum should stop unlocking anything, not make the rest unreadable.
    /// </summary>
    public async Task<IReadOnlyList<PermissionCode>> GetAllAsync(CancellationToken cancellationToken)
    {
        var rows = await _dbContext.PermissionCodes.AsNoTracking().ToListAsync(cancellationToken);
        return [.. rows
            .Select(row => Enum.TryParse<ApplicationPermission>(row.Permission, out var permission)
                ? new PermissionCode(permission, row.Code, row.CreatedAtUtc)
                : null)
            .OfType<PermissionCode>()];
    }

    /// <summary>
    /// Writes the code over whatever that permission held, or puts the first one there. The row is
    /// rewritten rather than removed and remade, so a permission is never left without a code.
    /// </summary>
    public async Task SaveAsync(PermissionCode code, CancellationToken cancellationToken)
    {
        var name = code.Permission.ToString();
        var existing = await _dbContext.PermissionCodes
            .FirstOrDefaultAsync(row => row.Permission == name, cancellationToken);

        if (existing is null)
        {
            _dbContext.PermissionCodes.Add(new PermissionCodeEntity
            {
                Permission = name,
                Code = code.Code,
                CreatedAtUtc = code.CreatedAtUtc
            });
        }
        else
        {
            existing.Code = code.Code;
            existing.CreatedAtUtc = code.CreatedAtUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

namespace Orbit.Core.Permissions;

public interface IPermissionCodeRepository
{
    Task<IReadOnlyList<PermissionCode>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores the code for a permission, replacing whatever that permission had before. Whether a
    /// permission's code should be left alone is the caller's decision, not this one's.
    /// </summary>
    Task SaveAsync(PermissionCode code, CancellationToken cancellationToken);
}

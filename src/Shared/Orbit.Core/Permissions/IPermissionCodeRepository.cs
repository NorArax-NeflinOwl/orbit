namespace Orbit.Core.Permissions;

public interface IPermissionCodeRepository
{
    Task<IReadOnlyList<PermissionCode>> GetAllAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Stores a code for a permission that has none. Does nothing when one already exists - the codes
    /// are handed out, so minting a second one would quietly invalidate whatever somebody was told.
    /// </summary>
    Task AddIfAbsentAsync(PermissionCode code, CancellationToken cancellationToken);
}

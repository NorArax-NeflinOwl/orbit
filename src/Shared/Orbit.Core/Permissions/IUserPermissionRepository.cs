namespace Orbit.Core.Permissions;

public interface IUserPermissionRepository
{
    /// <summary>What this account has been granted. An account that has redeemed nothing gets an empty set, never null.</summary>
    Task<IReadOnlySet<ApplicationPermission>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Grants one permission. Granting one an account already holds changes nothing, and is not an error.</summary>
    Task GrantAsync(Guid userId, ApplicationPermission permission, CancellationToken cancellationToken);
}

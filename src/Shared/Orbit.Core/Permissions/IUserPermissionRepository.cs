namespace Orbit.Core.Permissions;

public interface IUserPermissionRepository
{
    /// <summary>What this account has been granted. An account that has redeemed nothing gets an empty set, never null.</summary>
    Task<IReadOnlySet<ApplicationPermission>> GetForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// What each of these accounts has been granted, keyed by account. Accounts holding nothing are
    /// absent from the result rather than present with an empty set. One query for a whole roster: a
    /// contact list asking per person would be back to a round trip each.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlySet<ApplicationPermission>>> GetForUsersAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);

    /// <summary>Grants one permission. Granting one an account already holds changes nothing, and is not an error.</summary>
    Task GrantAsync(Guid userId, ApplicationPermission permission, CancellationToken cancellationToken);
}

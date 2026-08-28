namespace Orbit.Core.Permissions;

/// <summary>
/// Whether an account is one other people can find at all. An account that has not unlocked
/// <see cref="ApplicationPermission.Contacts"/> is invisible in both directions: it cannot look anybody
/// up, and nobody's search turns it up either.
///
/// The gate on the endpoints (see PermissionPolicies) covers the first half. This covers the second,
/// which no policy can: being refused is about the caller, and being absent from a result is about
/// everybody else in it.
/// </summary>
public sealed class UserVisibility
{
    private readonly IUserPermissionRepository _userPermissionRepository;

    public UserVisibility(IUserPermissionRepository userPermissionRepository)
    {
        _userPermissionRepository = userPermissionRepository;
    }

    public async Task<bool> IsFindableAsync(Guid userId, CancellationToken cancellationToken)
    {
        var granted = await _userPermissionRepository.GetForUserAsync(userId, cancellationToken);
        return ApplicationPermission.Contacts.IsEffective(granted);
    }

    /// <summary>The ones among these that other people can find. One query, whatever the roster's size.</summary>
    public async Task<IReadOnlySet<Guid>> FindableAmongAsync(
        IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        var granted = await _userPermissionRepository.GetForUsersAsync(userIds, cancellationToken);
        return userIds
            .Where(userId => granted.TryGetValue(userId, out var held) && ApplicationPermission.Contacts.IsEffective(held))
            .ToHashSet();
    }
}

using Orbit.Core.Permissions;

namespace Orbit.Web.Services;

/// <summary>
/// What this account has been unlocked for, held in one place so the navigation and the Options page
/// agree about it. This is presentation only: hiding a page the API would refuse saves somebody a
/// pointless click, and the refusal itself is the server's (see PermissionPolicies in Orbit.Api).
/// </summary>
public sealed class UserPermissionState(UsersApiClient usersApiClient)
{
    private HashSet<ApplicationPermission> _granted = [];

    /// <summary>Raised after <see cref="RefreshAsync"/> changes what is held, so the layout can re-render its navigation.</summary>
    public event Action? Changed;

    public bool Has(ApplicationPermission permission) => _granted.Contains(permission);

    /// <summary>
    /// Leaves the previous answer in place when the call fails. A transient failure is not evidence that
    /// somebody lost a permission, and blanking the navigation on a dropped request would say it was.
    /// </summary>
    public async Task RefreshAsync()
    {
        IReadOnlyList<string> granted;
        try
        {
            granted = await usersApiClient.GetPermissionsAsync();
        }
        catch (HttpRequestException)
        {
            return;
        }

        _granted = [.. granted
            .Select(name => Enum.TryParse<ApplicationPermission>(name, out var permission) ? permission : (ApplicationPermission?)null)
            .Where(permission => permission is not null)
            .Select(permission => permission!.Value)];
        Changed?.Invoke();
    }
}

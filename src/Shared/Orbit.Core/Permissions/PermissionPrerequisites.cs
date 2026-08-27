namespace Orbit.Core.Permissions;

/// <summary>
/// What has to be unlocked before something else can be. Contacts is the one everything social rests
/// on: talking to somebody and handing something to somebody both begin with there being a somebody,
/// and an account that cannot see anyone has nobody to do either with.
///
/// The rule lives here rather than at each place that checks a permission, so the server's gate, the
/// code redemption and the client's own list cannot disagree about it.
/// </summary>
public static class PermissionPrerequisites
{
    /// <summary>What this permission needs first, or null when it stands on its own.</summary>
    public static ApplicationPermission? RequiredBefore(this ApplicationPermission permission) => permission switch
    {
        ApplicationPermission.Chat or ApplicationPermission.Sharing => ApplicationPermission.Contacts,
        _ => null
    };

    /// <summary>
    /// Whether the permission actually lets this account do anything: held, and everything it rests on
    /// held too. Checked on every read rather than only when a code is redeemed, so a prerequisite taken
    /// away stops what depends on it there and then.
    /// </summary>
    public static bool IsEffective(this ApplicationPermission permission, IReadOnlySet<ApplicationPermission> granted)
        => granted.Contains(permission)
            && (permission.RequiredBefore() is not { } required || granted.Contains(required));

    /// <summary>The permissions that actually apply, in enum order - what a reader is shown and what the gate uses.</summary>
    public static IReadOnlyList<ApplicationPermission> Effective(IReadOnlySet<ApplicationPermission> granted)
        => [.. Enum.GetValues<ApplicationPermission>().Where(permission => permission.IsEffective(granted))];
}

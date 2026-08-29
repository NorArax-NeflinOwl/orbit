using Orbit.Core.Permissions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Permissions;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A <see cref="UserPermissions"/> for a screen test that is not about permissions.
///
/// Nothing loaded means nothing hidden - see UserPermissions for why that is the direction - so a bare
/// one behaves as an account with everything unlocked, which is what almost every test here wants. The
/// ones that do care use <see cref="LockedTo"/>.
/// </summary>
internal static class UnlockedPermissions
{
    /// <summary>
    /// One whose server grants everything, so a screen that asks - directly or through
    /// EnsureLoadedAsync - is told yes.
    /// </summary>
    public static UserPermissions For(LocalStore localStore, FakeUsersServer? users = null)
    {
        if (users is null)
        {
            users = new FakeUsersServer();
            users.Granted.AddRange(Enum.GetValues<ApplicationPermission>().Select(permission => permission.ToString()));
        }

        return new UserPermissions(new UsersClient(users.ToHttpClient()), localStore);
    }

    /// <summary>
    /// One that has actually heard from the server, and heard only these. Awaited rather than
    /// constructed so the answer is really held: a screen asked before the first read gets "yes".
    /// </summary>
    public static async Task<UserPermissions> LockedTo(
        LocalStore localStore, params ApplicationPermission[] granted)
    {
        var users = new FakeUsersServer();
        users.Granted.AddRange(granted.Select(permission => permission.ToString()));

        var permissions = For(localStore, users);
        await permissions.RefreshAsync();
        return permissions;
    }
}

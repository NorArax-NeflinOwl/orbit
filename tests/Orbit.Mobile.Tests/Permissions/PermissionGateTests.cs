using Orbit.Core.Permissions;
using Orbit.Mobile.Api;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Permissions;

/// <summary>
/// What this account may use, as the phone decides it. The server is the one that refuses (see
/// PermissionPolicies in Orbit.Api); this only decides what is worth offering - so the interesting
/// cases are the two where the phone has to guess: before any answer has arrived, and after one that
/// could not be fetched.
/// </summary>
public sealed class PermissionGateTests
{
    [Fact]
    public void Nothing_is_hidden_before_the_first_answer_arrives()
    {
        using var localStore = new LocalStore();
        var permissions = UnlockedPermissions.For(localStore);

        // Hiding what somebody is entitled to is the worse of the two mistakes, and offering what the
        // server will refuse costs one tap and an honest message.
        Assert.True(permissions.Has(ApplicationPermission.Chat));
        Assert.False(permissions.IsKnown);
    }

    [Fact]
    public async Task What_the_server_did_not_grant_is_hidden()
    {
        using var localStore = new LocalStore();
        var permissions = await UnlockedPermissions.LockedTo(
            localStore, ApplicationPermission.Contacts, ApplicationPermission.Chat);

        Assert.True(permissions.Has(ApplicationPermission.Chat));
        Assert.False(permissions.Has(ApplicationPermission.Location));
        Assert.True(permissions.IsKnown);
    }

    /// <summary>
    /// The prerequisite is checked on every read rather than only when a code is redeemed, so contacts
    /// taken away stops the chat that rests on it there and then - see PermissionPrerequisites.
    /// </summary>
    [Fact]
    public async Task A_permission_whose_prerequisite_is_missing_does_not_count()
    {
        using var localStore = new LocalStore();
        var permissions = await UnlockedPermissions.LockedTo(localStore, ApplicationPermission.Chat);

        Assert.Contains(ApplicationPermission.Chat, permissions.Granted);
        Assert.False(permissions.Has(ApplicationPermission.Chat));
    }

    /// <summary>
    /// The reason this is kept at all. A phone that asked the server on every cold start would hide chat
    /// and the map from somebody who has both, every time they opened the app underground.
    /// </summary>
    [Fact]
    public async Task What_the_server_last_said_survives_a_restart_with_no_connection()
    {
        using var localStore = new LocalStore();
        await UnlockedPermissions.LockedTo(
            localStore, ApplicationPermission.Contacts, ApplicationPermission.Chat);

        using var users = new FakeUsersServer { IsUnreachable = true };
        var afterRestart = new UserPermissions(new UsersClient(users.ToHttpClient()), localStore);
        await afterRestart.LoadStoredAsync();
        await afterRestart.RefreshAsync();

        Assert.True(afterRestart.Has(ApplicationPermission.Chat));
        Assert.False(afterRestart.Has(ApplicationPermission.Location));
    }

    /// <summary>
    /// A dropped request is not evidence that somebody lost a permission, and emptying the navigation
    /// bar would say it was.
    /// </summary>
    [Fact]
    public async Task A_failed_read_leaves_the_previous_answer_alone()
    {
        using var localStore = new LocalStore();
        using var users = new FakeUsersServer();
        users.Granted.Add(nameof(ApplicationPermission.Contacts));
        users.Granted.Add(nameof(ApplicationPermission.Chat));

        var permissions = new UserPermissions(new UsersClient(users.ToHttpClient()), localStore);
        await permissions.RefreshAsync();

        users.IsUnreachable = true;
        await permissions.RefreshAsync();

        Assert.True(permissions.Has(ApplicationPermission.Chat));
    }

    [Fact]
    public async Task A_permission_the_server_stops_reporting_is_dropped()
    {
        using var localStore = new LocalStore();
        using var users = new FakeUsersServer();
        users.Granted.Add(nameof(ApplicationPermission.Contacts));
        users.Granted.Add(nameof(ApplicationPermission.Chat));

        var permissions = new UserPermissions(new UsersClient(users.ToHttpClient()), localStore);
        await permissions.RefreshAsync();
        Assert.True(permissions.Has(ApplicationPermission.Chat));

        users.Granted.Clear();
        await permissions.RefreshAsync();

        Assert.False(permissions.Has(ApplicationPermission.Chat));
    }
}

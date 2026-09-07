using Orbit.Core.Permissions;
using Xunit;

namespace Orbit.Api.Tests.Permissions;

/// <summary>
/// Which permissions a screen names. One rule for both clients - see PermissionListing - so the web's
/// table and the phone's list cannot come to say different things about the same account.
/// </summary>
public sealed class PermissionListingTests
{
    [Fact]
    public void Debugger_is_not_named_to_an_account_that_has_not_unlocked_it()
    {
        var listed = PermissionListing.ListedFor(new HashSet<ApplicationPermission> { ApplicationPermission.Contacts });

        Assert.DoesNotContain(ApplicationPermission.Debug, listed);
    }

    /// <summary>
    /// Everything else is named whether or not it is held: "Locked" beside a part of Orbit is the answer
    /// to "can I use this", and leaving them out would make the screen say the account has less than it
    /// could have.
    /// </summary>
    [Fact]
    public void Everything_else_is_named_whether_it_is_unlocked_or_not()
    {
        var listed = PermissionListing.ListedFor(new HashSet<ApplicationPermission>());

        Assert.Equal(
            [ApplicationPermission.Contacts, ApplicationPermission.Chat, ApplicationPermission.Sharing, ApplicationPermission.Location],
            listed);
    }

    [Fact]
    public void Debugger_is_named_once_it_has_been_unlocked()
    {
        var listed = PermissionListing.ListedFor(new HashSet<ApplicationPermission> { ApplicationPermission.Debug });

        Assert.Contains(ApplicationPermission.Debug, listed);
    }

    /// <summary>
    /// The guard on the enum itself: a permission added later is listed unless somebody decides
    /// otherwise, which is the safer default - a part of Orbit nobody can see is a part nobody unlocks.
    /// </summary>
    [Fact]
    public void Only_Debugger_is_ever_withheld()
    {
        var granted = new HashSet<ApplicationPermission>();

        var withheld = Enum.GetValues<ApplicationPermission>()
            .Where(permission => !PermissionListing.IsListed(permission, granted))
            .ToList();

        Assert.Equal([ApplicationPermission.Debug], withheld);
    }
}

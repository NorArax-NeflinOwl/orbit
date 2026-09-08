namespace Orbit.Core.Permissions;

/// <summary>
/// Which permissions a screen lists at all. One rule for both clients, beside
/// <see cref="PermissionPrerequisites"/> and for the same reason: the web's table and the phone's own
/// list would otherwise say different things about the same account.
///
/// <see cref="ApplicationPermission.Debug"/> is the only one ever left out, and only while it is
/// locked. It is not a part of Orbit to use but the way in to what Orbit says about itself, so a row
/// reading "Locked" beside the others advertises that there is a code somewhere for it - to every
/// account, on a screen everybody visits. Whoever is meant to have it is told the code; typing it in is
/// what makes the row appear, along with everything else it unlocks.
///
/// The box the code is typed into is not part of this and is never hidden by it: hiding the box from
/// the account that has unlocked everything else - exactly the account somebody hands that code to -
/// would make the code impossible to use.
/// </summary>
public static class PermissionListing
{
    public static IReadOnlyList<ApplicationPermission> ListedFor(IReadOnlySet<ApplicationPermission> granted)
        => [.. Enum.GetValues<ApplicationPermission>().Where(permission => IsListed(permission, granted))];

    public static bool IsListed(ApplicationPermission permission, IReadOnlySet<ApplicationPermission> granted)
        => permission != ApplicationPermission.Debug || granted.Contains(permission);
}

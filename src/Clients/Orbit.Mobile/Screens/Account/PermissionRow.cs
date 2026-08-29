using Orbit.Core.Permissions;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;

namespace Orbit.Mobile.Screens.Account;

/// <summary>
/// One part of Orbit on the account screen's permission list: what it is, what it lets this account do,
/// and whether it is unlocked - or, when it is not, what has to be unlocked before it can be.
///
/// The prerequisite is read from <see cref="PermissionPrerequisites"/> rather than restated here, so
/// the phone, the web and the server cannot disagree about which permission rests on which.
/// </summary>
/// <param name="Status">
/// Already in the reader's language: "Unlocked", or the name of what it needs first, or empty when it
/// is simply waiting for a code.
/// </param>
public sealed record PermissionRow(string Name, string Explanation, bool IsGranted, string Status)
{
    public static PermissionRow For(
        ApplicationPermission permission, IReadOnlySet<ApplicationPermission> granted, Translations translations)
        => new(
            LockedFeatureMessage.Describe(permission, translations),
            LockedFeatureMessage.For(permission, translations),
            granted.Contains(permission),
            Describe(permission, granted, translations));

    public bool HasStatus => Status.Length > 0;

    private static string Describe(
        ApplicationPermission permission, IReadOnlySet<ApplicationPermission> granted, Translations translations)
    {
        if (granted.Contains(permission))
        {
            return translations["Unlocked"];
        }

        return permission.RequiredBefore() is { } required && !granted.Contains(required)
            ? translations.Format("Needs {0}", LockedFeatureMessage.Describe(required, translations))
            : string.Empty;
    }
}

using Orbit.Core.Permissions;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens;

/// <summary>
/// What to show in place of a part of Orbit this account has not unlocked. The same wording as
/// Orbit.Web's Options page uses for the same permission, so somebody who reads one and then the other
/// is told the same thing twice rather than two different things.
///
/// Saying what is missing matters more here than it looks: an empty conversation list would be a lie,
/// because there may well be plenty to show once the permission is there.
/// </summary>
public static class LockedFeatureMessage
{
    public static string For(ApplicationPermission permission, Translations translations) => permission switch
    {
        ApplicationPermission.Contacts
            => translations["Finding other people, and being found by them. Everything below needs this first."],
        ApplicationPermission.Chat
            => translations["Conversations, with one person or with several."],
        ApplicationPermission.Location
            => translations["Recording where you are. Sharing it, or seeing somebody else's, also needs contacts."],
        ApplicationPermission.Debug
            => translations["What Orbit reports about itself - the Debugger settings, the captured log, and the detail behind an error."],
        _ => translations["Handing a note, task list, event or storage to somebody else."]
    };

    /// <summary>The permission's own name, for a list that has to distinguish them.</summary>
    public static string Describe(ApplicationPermission permission, Translations translations) => permission switch
    {
        ApplicationPermission.Contacts => translations["Contacts"],
        ApplicationPermission.Chat => translations["Chat"],
        ApplicationPermission.Location => translations["Location"],
        // Named for what it opens rather than for the enum: the permission is Debug, what a reader is
        // offered is the Debugger, and the browser's Options says the same word.
        ApplicationPermission.Debug => translations["Debugger"],
        _ => translations["Sharing"]
    };
}

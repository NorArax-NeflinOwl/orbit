using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// One member as the group's detail screen shows them, already carrying what may be offered for them.
/// The viewer's own role decides that and is the same for every row, so it is folded in here rather
/// than asked again per button - a row that knew only about itself would have every template reaching
/// back up for the answer.
/// </summary>
/// <param name="Description">Already in the reader's language, so the row itself needs no dictionary.</param>
public sealed record GroupMemberRow(
    Guid UserId, string DisplayName, string Role, bool IsSelf, bool ViewerIsAdmin, string Description,
    string RemovalLabel)
{
    public static GroupMemberRow From(
        LocalChatGroupMember member, Guid ownUserId, bool viewerIsAdmin, Translations translations)
    {
        var isSelf = member.UserId == ownUserId;
        var role = member.Role == "Admin" ? translations["Admin"] : translations["Member"];

        return new(
            member.UserId, member.DisplayName, member.Role, isSelf, viewerIsAdmin,
            isSelf ? $"{role} · {translations["You"]}" : role,
            isSelf ? translations["Leave group"] : translations["Remove"]);
    }

    public bool IsAdmin => Role == "Admin";

    /// <summary>
    /// Removing another member is an admin's to do; showing yourself out is anybody's. That is what the
    /// server says - see ChatGroup.RemoveMember, whose own comment records that requiring admin for both
    /// "left an ordinary member with no way out of a group at all", which is exactly what this screen
    /// did: it asked for admin whoever the subject was, so a member who wanted out had no button.
    ///
    /// The last admin is still refused while anyone remains, by the server. That is its call rather than
    /// this row's: the answer depends on who else is in the group and what they are.
    /// </summary>
    public bool CanBeRemoved => ViewerIsAdmin || IsSelf;

    public bool CanBePromoted => ViewerIsAdmin && !IsAdmin;

    public bool CanBeDemoted => ViewerIsAdmin && IsAdmin;
}

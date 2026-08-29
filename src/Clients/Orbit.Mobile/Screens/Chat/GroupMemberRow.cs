using Orbit.Mobile.Data;

namespace Orbit.Mobile.Screens.Chat;

/// <summary>
/// One member as the group's detail screen shows them, already carrying what may be offered for them.
/// The viewer's own role decides that and is the same for every row, so it is folded in here rather
/// than asked again per button - a row that knew only about itself would have every template reaching
/// back up for the answer.
/// </summary>
public sealed record GroupMemberRow(Guid UserId, string DisplayName, string Role, bool IsSelf, bool ViewerIsAdmin)
{
    public static GroupMemberRow From(LocalChatGroupMember member, Guid ownUserId, bool viewerIsAdmin)
        => new(member.UserId, member.DisplayName, member.Role, member.UserId == ownUserId, viewerIsAdmin);

    public bool IsAdmin => Role == "Admin";

    /// <summary>
    /// Only an admin may change a group's membership - including removing themselves, which is how
    /// leaving happens. See ChatGroup.RemoveMember: the actor has to be an admin whoever the subject is.
    /// </summary>
    public bool CanBeRemoved => ViewerIsAdmin;

    public bool CanBePromoted => ViewerIsAdmin && !IsAdmin;

    public bool CanBeDemoted => ViewerIsAdmin && IsAdmin;

    /// <summary>What the row says about them: their role, and which one of them is the reader.</summary>
    public string Description => IsSelf ? $"{Role} · you" : Role;
}

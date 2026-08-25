namespace Orbit.Core.Chat.Groups;

/// <summary>
/// What a member is allowed to do inside a group chat. Deliberately two values rather than a set of
/// individual permissions: every capability asked of this feature falls on one side or the other of the
/// same line, and a permission matrix nobody varies is a lot of machinery to keep correct for nothing.
/// </summary>
public enum ChatGroupRole
{
    /// <summary>Can read the group, post to it, and delete messages they sent themselves.</summary>
    Member = 0,

    /// <summary>Everything a member can do, plus adding and removing members, changing roles, and deleting anyone's messages.</summary>
    Admin = 1
}

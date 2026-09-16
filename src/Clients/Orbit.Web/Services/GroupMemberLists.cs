using Orbit.Contracts.Tasks;

namespace Orbit.Web.Services;

/// <summary>
/// Why one of a group's member lists can or cannot be written in from the group's own editor - see
/// <see cref="GroupMemberLists"/>. Said as a reason rather than a bare "no", because every one of these has
/// somewhere else the reader can go and do it.
/// </summary>
public enum WhyAMemberIsReadOnly
{
    /// <summary>It can be written in.</summary>
    None,

    /// <summary>
    /// Sealed. Its entries live inside a payload only its own editor unseals, and writing them from here
    /// would mean sealing another list's contents from inside this one - see PrivateContentSealer.
    /// </summary>
    Sealed,

    /// <summary>Shared to read, so it is not this reader's to change - anywhere, not only here.</summary>
    SharedToRead,

    /// <summary>Somebody else holds its edit lock, exactly as they can hold this group's own.</summary>
    HeldBySomebodyElse
}

/// <summary>
/// The lists a group gathers, as the group's editor holds them - the answer to "a child's entries
/// cannot be renamed, added or removed from there", which is what the user asked to be able to do.
///
/// <b>Direct members only.</b> A member that is itself a group gathers lists of its own, and those are
/// edited by opening it: a form that unfolded a whole tree would be a form whose length nobody can
/// predict, and the light view is what reads a tree end to end (see TaskListChecklist.BuildSections).
///
/// <b>A list is named once</b> however many entries point at it, and the group itself is never among its
/// own members - a list that links back to itself would otherwise appear twice in one form, with two
/// sets of boxes writing over each other.
/// </summary>
public static class GroupMemberLists
{
    /// <summary>
    /// The ids the group's entries point at, in the order the entries do and without repeats.
    /// <paramref name="groupId"/> is left out of its own members.
    /// </summary>
    public static IReadOnlyList<Guid> IdsUnder(Guid groupId, IEnumerable<IReadOnlyList<Guid>> linkedIdsPerEntry)
        => [.. linkedIdsPerEntry.SelectMany(ids => ids).Where(id => id != groupId).Distinct()];

    /// <summary>
    /// Why this member cannot be written in here, or <see cref="WhyAMemberIsReadOnly.None"/> when it
    /// can. <paramref name="lockedByUserName"/> is who holds its edit lock, or null for nobody.
    ///
    /// Asked in this order on purpose: being sealed is about the list itself, being shared to read is
    /// about this reader, and a lock is about this moment. The first two do not go away by waiting, so
    /// saying "somebody is editing it" about a list this reader could never edit would send them back
    /// to try again for nothing.
    /// </summary>
    public static WhyAMemberIsReadOnly WhyReadOnly(TaskDto member, string? lockedByUserName)
    {
        if (member.IsPrivate)
        {
            return WhyAMemberIsReadOnly.Sealed;
        }

        if (member.IsShared && !SharedItemAccess.For(member.IsShared, member.AccessLevel).CanEdit)
        {
            return WhyAMemberIsReadOnly.SharedToRead;
        }

        return lockedByUserName is null ? WhyAMemberIsReadOnly.None : WhyAMemberIsReadOnly.HeldBySomebodyElse;
    }
}

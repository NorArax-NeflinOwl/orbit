using Orbit.Contracts.Tasks;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// Which lists a group's editor holds beside its own, and which of them can be written in there - the
/// rules behind "a child's entries cannot be renamed, added or removed from there", which is what the
/// user asked to be able to do.
/// </summary>
public sealed class GroupMemberListsTests
{
    private static readonly Guid GroupId = Guid.NewGuid();
    private static readonly Guid First = Guid.NewGuid();
    private static readonly Guid Second = Guid.NewGuid();

    private static TaskDto AList(bool isPrivate = false, bool isShared = false, string accessLevel = "ReadOnly")
        => new(
            Guid.NewGuid(), "Errands", [], IsCompleted: false, IsGroup: false, IsPrivate: isPrivate,
            EncryptedContent: null, CreatedAtUtc: DateTimeOffset.UtcNow, UpdatedAtUtc: DateTimeOffset.UtcNow,
            IsShared: isShared, SharedByUserName: null, AccessLevel: accessLevel, OriginalOwnerUserId: null);

    [Fact]
    public void A_group_holds_the_lists_its_entries_point_at()
    {
        var members = GroupMemberLists.IdsUnder(GroupId, [[First], [Second]]);

        Assert.Equal([First, Second], members);
    }

    /// <summary>
    /// A list named by two entries is one list. Named twice it would appear twice in one form, with two
    /// sets of boxes writing over each other.
    /// </summary>
    [Fact]
    public void A_list_two_entries_point_at_is_held_once()
    {
        var members = GroupMemberLists.IdsUnder(GroupId, [[First], [First, Second]]);

        Assert.Equal([First, Second], members);
    }

    /// <summary>A list that links back to itself is not its own member.</summary>
    [Fact]
    public void A_group_is_never_among_its_own_members()
    {
        var members = GroupMemberLists.IdsUnder(GroupId, [[GroupId, First]]);

        Assert.Equal([First], members);
    }

    [Fact]
    public void An_ordinary_member_can_be_written_in()
    {
        Assert.Equal(WhyAMemberIsReadOnly.None, GroupMemberLists.WhyReadOnly(AList(), lockedByUserName: null));
    }

    /// <summary>
    /// A sealed member is read-only here: its entries live inside a payload only its own editor unseals,
    /// and writing them from the group would mean sealing another list's contents from inside this one.
    /// </summary>
    [Fact]
    public void A_sealed_member_is_read_only()
    {
        Assert.Equal(
            WhyAMemberIsReadOnly.Sealed,
            GroupMemberLists.WhyReadOnly(AList(isPrivate: true), lockedByUserName: null));
    }

    [Fact]
    public void A_member_shared_to_read_is_read_only()
    {
        Assert.Equal(
            WhyAMemberIsReadOnly.SharedToRead,
            GroupMemberLists.WhyReadOnly(AList(isShared: true, accessLevel: "ReadOnly"), lockedByUserName: null));
    }

    /// <summary>One shared with permission to change it is editable here like any other.</summary>
    [Fact]
    public void A_member_shared_with_editing_can_be_written_in()
    {
        Assert.Equal(
            WhyAMemberIsReadOnly.None,
            GroupMemberLists.WhyReadOnly(AList(isShared: true, accessLevel: "CanEdit"), lockedByUserName: null));
    }

    [Fact]
    public void A_member_somebody_else_is_editing_is_read_only_for_now()
    {
        Assert.Equal(
            WhyAMemberIsReadOnly.HeldBySomebodyElse,
            GroupMemberLists.WhyReadOnly(AList(), lockedByUserName: "Ola"));
    }

    /// <summary>
    /// Being sealed is asked first: a lock goes away by waiting and the other two do not, so telling
    /// somebody "she is editing it" about a list they could never edit sends them back for nothing.
    /// </summary>
    [Fact]
    public void What_cannot_change_is_said_before_what_can()
    {
        Assert.Equal(
            WhyAMemberIsReadOnly.Sealed,
            GroupMemberLists.WhyReadOnly(AList(isPrivate: true), lockedByUserName: "Ola"));
    }
}

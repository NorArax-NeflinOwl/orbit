using Orbit.Core.Abstractions;
using Orbit.Core.Chat.Groups;
using Xunit;

namespace Orbit.Api.Tests.Chat;

/// <summary>
/// Covers who may do what inside a group. Every rule lives on ChatGroup itself, so these exercise it
/// directly rather than through a handler - the handlers only decide whether the caller can see the
/// group at all.
/// </summary>
public sealed class ChatGroupTests
{
    private readonly Guid _creatorId = Guid.NewGuid();
    private readonly Guid _memberId = Guid.NewGuid();
    private readonly Guid _outsiderId = Guid.NewGuid();

    [Fact]
    public void The_creator_starts_as_the_groups_admin()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");

        // A group whose only member couldn't manage it would be stuck from the moment it existed.
        Assert.True(group.IsAdmin(_creatorId));
        Assert.Equal("Weekend trip", group.Name);
    }

    [Fact]
    public void A_group_needs_a_name()
        => Assert.Throws<InvalidRequestException>(() => ChatGroup.Create(_creatorId, "   "));

    [Fact]
    public void An_admin_adds_members_as_ordinary_members()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");

        group.AddMember(_creatorId, _memberId);

        Assert.True(group.IsMember(_memberId));
        Assert.False(group.IsAdmin(_memberId));
    }

    [Fact]
    public void Adding_someone_already_in_the_group_changes_nothing()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);
        group.ChangeRole(_creatorId, _memberId, ChatGroupRole.Admin);

        group.AddMember(_creatorId, _memberId);

        // Notably it must not demote them back to member: the end state asked for is "they are in".
        Assert.Single(group.Members, member => member.UserId == _memberId);
        Assert.True(group.IsAdmin(_memberId));
    }

    [Fact]
    public void An_ordinary_member_cannot_manage_the_group()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        Assert.Throws<InvalidRequestException>(() => group.AddMember(_memberId, _outsiderId));
        // Removing somebody else, that is. Showing themselves out is not managing the group - see
        // An_ordinary_member_may_leave below.
        Assert.Throws<InvalidRequestException>(() => group.RemoveMember(_memberId, _creatorId));
        Assert.Throws<InvalidRequestException>(() => group.ChangeRole(_memberId, _memberId, ChatGroupRole.Admin));
        Assert.Throws<InvalidRequestException>(() => group.Rename(_memberId, "Hijacked"));
    }

    [Fact]
    public void Someone_outside_the_group_cannot_manage_it_either()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");

        Assert.Throws<InvalidRequestException>(() => group.AddMember(_outsiderId, _outsiderId));
    }

    [Fact]
    public void An_admin_promotes_and_demotes_members()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        group.ChangeRole(_creatorId, _memberId, ChatGroupRole.Admin);
        Assert.True(group.IsAdmin(_memberId));

        group.ChangeRole(_creatorId, _memberId, ChatGroupRole.Member);
        Assert.False(group.IsAdmin(_memberId));
        Assert.True(group.IsMember(_memberId));
    }

    [Fact]
    public void The_last_admin_cannot_be_demoted()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        // Otherwise the group is left with nobody who can add, remove, or promote anyone - unmanageable
        // and unrecoverable from inside.
        var exception = Assert.Throws<InvalidRequestException>(() => group.ChangeRole(_creatorId, _creatorId, ChatGroupRole.Member));
        Assert.Contains("at least one admin", exception.Message);
    }

    /// <summary>
    /// This used to be refused ("promote someone else first"), which left the one person with the most
    /// say in a group as the one person who could not walk out of it. What the refusal protected - a
    /// group with people in it and nobody able to manage it - is prevented by handing it over instead.
    /// </summary>
    [Fact]
    public void The_only_admin_leaving_one_other_member_hands_the_group_to_them()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        group.Leave(_creatorId);

        Assert.False(group.IsMember(_creatorId));
        Assert.True(group.IsAdmin(_memberId));
    }

    [Fact]
    public void The_only_admin_leaving_many_members_hands_the_group_to_the_longest_standing()
    {
        var admin = Guid.NewGuid();
        var newest = Guid.NewGuid();
        var oldest = Guid.NewGuid();
        var middle = Guid.NewGuid();
        var group = GroupOf(
            (admin, ChatGroupRole.Admin, 30), (newest, ChatGroupRole.Member, 1),
            (oldest, ChatGroupRole.Member, 20), (middle, ChatGroupRole.Member, 10));

        group.Leave(admin);

        // Exactly one new admin, and it is the member who has been there longest - the same person an
        // account deletion would have promoted, since both go through ChooseSuccessor.
        Assert.Equal(oldest, Assert.Single(group.Members, member => member.Role == ChatGroupRole.Admin).UserId);
        Assert.Equal(3, group.Members.Count);
    }

    /// <summary>
    /// Removing yourself through the roster's route is leaving too. Installed phone builds leave that
    /// way, so it must not keep the old refusal while the dedicated route has dropped it.
    /// </summary>
    [Fact]
    public void Removing_yourself_is_leaving_and_follows_the_same_rules()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        group.RemoveMember(_creatorId, _creatorId);

        Assert.False(group.IsMember(_creatorId));
        Assert.True(group.IsAdmin(_memberId));
    }

    [Fact]
    public void An_admin_among_several_leaves_without_anybody_being_promoted()
    {
        var leaving = Guid.NewGuid();
        var otherAdmin = Guid.NewGuid();
        var member = Guid.NewGuid();
        var group = GroupOf(
            (leaving, ChatGroupRole.Admin, 30), (otherAdmin, ChatGroupRole.Admin, 5), (member, ChatGroupRole.Member, 20));

        group.Leave(leaving);

        // The group can still be managed, so the longest-standing member is not handed anything.
        Assert.True(group.IsAdmin(otherAdmin));
        Assert.False(group.IsAdmin(member));
    }

    [Fact]
    public void The_leaving_admin_may_name_who_takes_over()
    {
        var admin = Guid.NewGuid();
        var oldest = Guid.NewGuid();
        var chosen = Guid.NewGuid();
        var group = GroupOf((admin, ChatGroupRole.Admin, 30), (oldest, ChatGroupRole.Member, 20), (chosen, ChatGroupRole.Member, 1));

        group.Leave(admin, chosen);

        // The named person, not the one the automatic rule would have picked.
        Assert.True(group.IsAdmin(chosen));
        Assert.False(group.IsAdmin(oldest));
        Assert.False(group.IsMember(admin));
    }

    /// <summary>
    /// Refused rather than replaced by the automatic choice: the leaver asked for a particular person,
    /// and once they are out they cannot undo somebody else being handed the group.
    /// </summary>
    [Fact]
    public void A_successor_who_is_not_in_the_group_is_refused_and_nothing_changes()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        var exception = Assert.Throws<InvalidRequestException>(() => group.Leave(_creatorId, _outsiderId));

        Assert.Contains("isn't in this group", exception.Message);
        Assert.True(group.IsAdmin(_creatorId));
        Assert.False(group.IsAdmin(_memberId));
    }

    [Fact]
    public void The_leaver_cannot_name_themselves_to_take_over()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        Assert.Throws<InvalidRequestException>(() => group.Leave(_creatorId, _creatorId));
        Assert.True(group.IsMember(_creatorId));
    }

    /// <summary>Naming a successor promotes somebody, which is an admin's act and not a plain member's.</summary>
    [Fact]
    public void A_plain_member_cannot_name_a_successor_on_the_way_out()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);
        group.AddMember(_creatorId, _outsiderId);

        Assert.Throws<InvalidRequestException>(() => group.Leave(_memberId, _outsiderId));
        Assert.True(group.IsMember(_memberId));
        Assert.False(group.IsAdmin(_outsiderId));
    }

    [Fact]
    public void Removing_another_admin_is_still_an_admins_to_do_and_keeps_the_actor_in_charge()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);
        group.ChangeRole(_creatorId, _memberId, ChatGroupRole.Admin);

        group.RemoveMember(_creatorId, _memberId);

        Assert.False(group.IsMember(_memberId));
        Assert.True(group.IsAdmin(_creatorId));
    }

    [Fact]
    public void An_ordinary_member_may_leave()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        // Requiring admin for this too left a member with no way out of a group at all: they could not
        // remove themselves, and nobody else was obliged to.
        group.RemoveMember(_memberId, _memberId);

        Assert.False(group.IsMember(_memberId));
        Assert.True(group.IsMember(_creatorId));
    }

    [Fact]
    public void Somebody_outside_the_group_cannot_leave_it()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");

        // Not an error worth hiding behind a no-op: asking to leave something you are not in is a
        // mistaken request, not an achieved end state.
        Assert.Throws<InvalidRequestException>(() => group.RemoveMember(_outsiderId, _outsiderId));
    }

    [Fact]
    public void The_last_person_out_empties_the_group_rather_than_being_kept_in_it()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");

        // The sole admin of a group with nobody else in it has no one to promote, so refusing here
        // would strand them in a group forever. The group empties instead, the same end
        // RemoveDeletedAccount reaches - and the handler deletes it.
        group.RemoveMember(_creatorId, _creatorId);

        Assert.True(group.IsEmpty);
    }

    [Fact]
    public void An_admin_may_step_down_once_someone_else_can_take_over()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);
        group.ChangeRole(_creatorId, _memberId, ChatGroupRole.Admin);

        group.RemoveMember(_creatorId, _creatorId);

        Assert.False(group.IsMember(_creatorId));
        Assert.True(group.IsAdmin(_memberId));
    }

    [Fact]
    public void Changing_the_role_of_someone_outside_the_group_is_refused()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");

        Assert.Throws<InvalidRequestException>(() => group.ChangeRole(_creatorId, _outsiderId, ChatGroupRole.Admin));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void A_member_may_delete_only_their_own_messages(bool ownMessage, bool expected)
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);
        var senderId = ownMessage ? _memberId : _creatorId;

        Assert.Equal(expected, group.CanDeleteMessageFrom(_memberId, senderId));
    }

    [Fact]
    public void An_admin_may_delete_anyones_message()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        Assert.True(group.CanDeleteMessageFrom(_creatorId, _memberId));
        Assert.True(group.CanDeleteMessageFrom(_creatorId, _creatorId));
    }

    [Fact]
    public void Someone_outside_the_group_may_delete_nothing_in_it()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        // Including a message they sent themselves before being removed from the group.
        Assert.False(group.CanDeleteMessageFrom(_outsiderId, _outsiderId));
        Assert.False(group.CanDeleteMessageFrom(_outsiderId, _memberId));
    }

    /// <summary>A group whose members joined the given number of days ago, so who has been there longest is known.</summary>
    private static ChatGroup GroupOf(params (Guid UserId, ChatGroupRole Role, int JoinedDaysAgo)[] members)
        => ChatGroup.FromPersistence(
            Guid.NewGuid(), "Team", members[0].UserId, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            [.. members.Select(member => new ChatGroupMembership(
                Guid.Empty, member.UserId, member.Role, DateTimeOffset.UtcNow.AddDays(-member.JoinedDaysAgo)))]);
}

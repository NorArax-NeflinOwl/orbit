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

    [Fact]
    public void The_last_admin_cannot_be_removed()
    {
        var group = ChatGroup.Create(_creatorId, "Weekend trip");
        group.AddMember(_creatorId, _memberId);

        Assert.Throws<InvalidRequestException>(() => group.RemoveMember(_creatorId, _creatorId));
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
}

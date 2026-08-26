using Orbit.Core.Chat.Groups;
using Xunit;

namespace Orbit.Api.Tests.Chat;

/// <summary>
/// Losing a member to a deleted account is the one departure nobody performs and nothing may refuse, so
/// it breaks rules RemoveMember enforces. These pin down what it does instead - above all that it never
/// leaves a group without an admin, which is the state every other rule in ChatGroup exists to prevent.
/// </summary>
public sealed class ChatGroupDeletedAccountTests
{
    [Fact]
    public void An_ordinary_member_leaving_changes_nothing_else()
    {
        var admin = Guid.NewGuid();
        var member = Guid.NewGuid();
        var group = GroupWith(admin, (member, ChatGroupRole.Member));

        group.RemoveDeletedAccount(member);

        Assert.False(group.IsMember(member));
        Assert.True(group.IsAdmin(admin));
        Assert.Single(group.Members);
    }

    [Fact]
    public void The_only_admin_leaving_promotes_the_longest_standing_member()
    {
        // RemoveMember would refuse this outright ("promote someone else first"), but an account is gone
        // and there is nobody left to do the promoting - so the group does it rather than being stranded.
        var admin = Guid.NewGuid();
        var newer = Guid.NewGuid();
        var oldest = Guid.NewGuid();
        var group = ChatGroup.FromPersistence(
            Guid.NewGuid(), "Team", admin, DateTimeOffset.UtcNow,
            [
                Membership(admin, ChatGroupRole.Admin, joinedDaysAgo: 10),
                Membership(newer, ChatGroupRole.Member, joinedDaysAgo: 1),
                Membership(oldest, ChatGroupRole.Member, joinedDaysAgo: 5)
            ]);

        group.RemoveDeletedAccount(admin);

        Assert.True(group.IsAdmin(oldest));
        Assert.False(group.IsAdmin(newer));
        Assert.False(group.IsMember(admin));
    }

    [Fact]
    public void An_admin_leaving_promotes_nobody_while_another_admin_remains()
    {
        var leaving = Guid.NewGuid();
        var otherAdmin = Guid.NewGuid();
        var member = Guid.NewGuid();
        var group = GroupWith(leaving, (otherAdmin, ChatGroupRole.Admin), (member, ChatGroupRole.Member));

        group.RemoveDeletedAccount(leaving);

        Assert.True(group.IsAdmin(otherAdmin));
        // The group can still be managed, so nothing should have been handed to the ordinary member.
        Assert.False(group.IsAdmin(member));
    }

    [Fact]
    public void The_last_member_leaving_empties_the_group_rather_than_promoting_anyone()
    {
        var onlyMember = Guid.NewGuid();
        var group = GroupWith(onlyMember);

        group.RemoveDeletedAccount(onlyMember);

        Assert.True(group.IsEmpty);
        Assert.Empty(group.Members);
    }

    [Fact]
    public void Removing_someone_who_was_never_in_the_group_does_nothing()
    {
        var admin = Guid.NewGuid();
        var group = GroupWith(admin);

        group.RemoveDeletedAccount(Guid.NewGuid());

        Assert.True(group.IsAdmin(admin));
        Assert.Single(group.Members);
    }

    [Fact]
    public void Promotion_is_deterministic_when_two_members_joined_at_the_same_moment()
    {
        // Two people added in one request share a timestamp; the choice still has to be repeatable
        // rather than depending on the order rows came back from the database.
        var admin = Guid.NewGuid();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var joinedAt = DateTimeOffset.UtcNow.AddDays(-1);

        var promoted = new List<Guid>();
        foreach (var ordering in new[] { new[] { first, second }, new[] { second, first } })
        {
            var group = ChatGroup.FromPersistence(
                Guid.NewGuid(), "Team", admin, DateTimeOffset.UtcNow,
                [
                    new ChatGroupMembership(Guid.Empty, admin, ChatGroupRole.Admin, joinedAt),
                    new ChatGroupMembership(Guid.Empty, ordering[0], ChatGroupRole.Member, joinedAt),
                    new ChatGroupMembership(Guid.Empty, ordering[1], ChatGroupRole.Member, joinedAt)
                ]);

            group.RemoveDeletedAccount(admin);
            promoted.Add(group.Members.Single(member => member.Role == ChatGroupRole.Admin).UserId);
        }

        Assert.Equal(promoted[0], promoted[1]);
    }

    [Fact]
    public void A_promoted_member_can_immediately_manage_the_group()
    {
        // The point of promoting at all: the group must not be left unable to add or remove anyone.
        var admin = Guid.NewGuid();
        var survivor = Guid.NewGuid();
        var group = GroupWith(admin, (survivor, ChatGroupRole.Member));

        group.RemoveDeletedAccount(admin);
        group.AddMember(survivor, Guid.NewGuid());

        Assert.Equal(2, group.Members.Count);
    }

    private static ChatGroup GroupWith(Guid adminUserId, params (Guid UserId, ChatGroupRole Role)[] others)
    {
        var members = new List<ChatGroupMembership> { Membership(adminUserId, ChatGroupRole.Admin, joinedDaysAgo: 10) };
        members.AddRange(others.Select(other => Membership(other.UserId, other.Role, joinedDaysAgo: 1)));
        return ChatGroup.FromPersistence(Guid.NewGuid(), "Team", adminUserId, DateTimeOffset.UtcNow, members);
    }

    private static ChatGroupMembership Membership(Guid userId, ChatGroupRole role, int joinedDaysAgo)
        => new(Guid.Empty, userId, role, DateTimeOffset.UtcNow.AddDays(-joinedDaysAgo));
}

using Orbit.Core.Abstractions;

namespace Orbit.Core.Chat.Groups;

/// <summary>
/// A named chat with more than two people in it. Membership and roles live here rather than in the
/// handlers so every rule about who may do what is stated once, in the place that can actually enforce
/// it - each method takes the id of whoever is asking and refuses if they aren't entitled.
///
/// The group owns no messages: a group message is fanned out into ordinary per-recipient rows so it
/// stays end-to-end encrypted under the pairwise keys people already have (see ChatMessage.CreateForGroup
/// and SendGroupMessageCommandHandler). What that costs is spelled out there.
/// </summary>
public sealed class ChatGroup
{
    private readonly List<ChatGroupMembership> _members;

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    public IReadOnlyList<ChatGroupMembership> Members => _members;

    private ChatGroup(Guid id, string name, Guid createdByUserId, DateTimeOffset createdAtUtc, List<ChatGroupMembership> members)
    {
        Id = id;
        Name = name;
        CreatedByUserId = createdByUserId;
        CreatedAtUtc = createdAtUtc;
        _members = members;
    }

    /// <summary>The creator is the first admin - a group with nobody able to manage it would be stuck from the start.</summary>
    public static ChatGroup Create(Guid createdByUserId, string name)
    {
        var trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            throw new InvalidRequestException("A group needs a name.");
        }

        var now = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();
        return new ChatGroup(
            id, trimmedName, createdByUserId, now,
            [new ChatGroupMembership(id, createdByUserId, ChatGroupRole.Admin, now)]);
    }

    /// <summary>Rebuilds a group from already-persisted values, bypassing every rule below.</summary>
    public static ChatGroup FromPersistence(
        Guid id, string name, Guid createdByUserId, DateTimeOffset createdAtUtc, IReadOnlyList<ChatGroupMembership> members)
        => new(id, name, createdByUserId, createdAtUtc, [.. members]);

    public ChatGroupMembership? FindMember(Guid userId) => _members.FirstOrDefault(member => member.UserId == userId);

    public bool IsMember(Guid userId) => FindMember(userId) is not null;

    public bool IsAdmin(Guid userId) => FindMember(userId)?.Role == ChatGroupRole.Admin;

    public void Rename(Guid actorUserId, string name)
    {
        RequireAdmin(actorUserId);
        var trimmedName = name.Trim();
        if (trimmedName.Length == 0)
        {
            throw new InvalidRequestException("A group needs a name.");
        }

        Name = trimmedName;
    }

    /// <summary>Adding someone already in the group is a no-op rather than an error - the end state is what was asked for.</summary>
    public void AddMember(Guid actorUserId, Guid userId)
    {
        RequireAdmin(actorUserId);
        if (IsMember(userId))
        {
            return;
        }

        _members.Add(new ChatGroupMembership(Id, userId, ChatGroupRole.Member, DateTimeOffset.UtcNow));
    }

    /// <summary>
    /// Removing the last admin is refused: it would leave a group nobody can add to, remove from, or
    /// promote in. An admin removing themselves is fine as long as another one remains.
    /// </summary>
    public void RemoveMember(Guid actorUserId, Guid userId)
    {
        RequireAdmin(actorUserId);
        var member = FindMember(userId);
        if (member is null)
        {
            return;
        }

        if (member.Role == ChatGroupRole.Admin && AdminCount == 1)
        {
            throw new InvalidRequestException("A group needs at least one admin - promote someone else first.");
        }

        _members.Remove(member);
    }

    /// <summary>Promotes or demotes a member. Demoting the last admin is refused for the same reason removing them is.</summary>
    public void ChangeRole(Guid actorUserId, Guid userId, ChatGroupRole role)
    {
        RequireAdmin(actorUserId);
        var member = FindMember(userId)
            ?? throw new InvalidRequestException("That person isn't in this group.");

        if (member.Role == role)
        {
            return;
        }

        if (member.Role == ChatGroupRole.Admin && AdminCount == 1)
        {
            throw new InvalidRequestException("A group needs at least one admin - promote someone else first.");
        }

        _members.Remove(member);
        _members.Add(member with { Role = role });
    }

    /// <summary>
    /// Whether actorUserId may delete a message sent by senderUserId: their own always, anyone's if they
    /// administer the group. The single place this rule is expressed - see DeleteChatMessageCommandHandler.
    /// </summary>
    public bool CanDeleteMessageFrom(Guid actorUserId, Guid senderUserId)
        => IsMember(actorUserId) && (actorUserId == senderUserId || IsAdmin(actorUserId));

    private int AdminCount => _members.Count(member => member.Role == ChatGroupRole.Admin);

    private void RequireAdmin(Guid actorUserId)
    {
        if (!IsAdmin(actorUserId))
        {
            throw new InvalidRequestException("Only a group admin can do that.");
        }
    }
}

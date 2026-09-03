namespace Orbit.Data.Entities;

/// <summary>One person's membership of one group. Role is stored by name - see Orbit.Core.Chat.Groups.ChatGroupRole.</summary>
public sealed class ChatGroupMemberEntity
{
    public Guid Id { get; set; }
    public Guid GroupId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTimeOffset JoinedAtUtc { get; set; }

    /// <summary>Put away by this member - see Orbit.Core.Chat.Groups.ChatGroupMembership.IsArchived.</summary>
    public bool IsArchived { get; set; }
}

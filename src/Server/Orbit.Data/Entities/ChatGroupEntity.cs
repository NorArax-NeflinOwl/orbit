namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a group chat, mapped separately from <see cref="Orbit.Core.Chat.Groups.ChatGroup"/>
/// so schema changes don't force changes onto domain logic, and vice versa. The group owns no messages:
/// those are ordinary ChatMessageEntity rows tagged with GroupId - see ChatMessage.CreateForGroup.
/// </summary>
public sealed class ChatGroupEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public List<ChatGroupMemberEntity> Members { get; set; } = [];
}

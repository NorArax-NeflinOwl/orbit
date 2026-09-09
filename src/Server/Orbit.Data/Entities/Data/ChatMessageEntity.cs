namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of an encrypted chat message, mapped separately from
/// <see cref="Orbit.Core.Chat.ChatMessage"/> so schema changes don't force changes onto domain logic,
/// and vice versa. CiphertextBase64/NonceBase64 are opaque to the server - see ChatMessage's own doc
/// comment.
/// </summary>
public sealed class ChatMessageEntity
{
    public Guid Id { get; set; }
    public Guid SenderUserId { get; set; }
    public Guid RecipientUserId { get; set; }
    public string CiphertextBase64 { get; set; } = string.Empty;
    public string NonceBase64 { get; set; } = string.Empty;
    public DateTimeOffset SentAtUtc { get; set; }

    /// <summary>Null until the recipient's chat window has polled this conversation at least once.</summary>
    public DateTimeOffset? ReadAtUtc { get; set; }

    public bool IsEdited { get; set; }

    /// <summary>Null until the sender edits this message at least once - see EditMessageCommandHandler.</summary>
    public DateTimeOffset? EditedAtUtc { get; set; }

    /// <summary>The group this was posted to, or null for a one-to-one message.</summary>
    public Guid? GroupId { get; set; }

    /// <summary>Shared by every per-recipient copy of one group posting - see Orbit.Core.Chat.ChatMessage.GroupMessageId.</summary>
    public Guid? GroupMessageId { get; set; }

    /// <summary>
    /// True for a copy re-encrypted after the fact for somebody who joined the group later - see
    /// Orbit.Core.Chat.ChatMessage.IsSharedHistory, and GetGroupReceiptsAsync, which leaves these out so
    /// a backfill does not turn a sender's read message back into an unread one.
    /// </summary>
    public bool IsSharedHistory { get; set; }

    /// <summary>When this message was deleted, or null - see Orbit.Core.Chat.ChatMessage.DeletedAtUtc.</summary>
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>Who deleted it, which is not always its sender - see the domain property.</summary>
    public Guid? DeletedByUserId { get; set; }

    /// <summary>
    /// The share this message is the invitation to, or null - see
    /// Orbit.Core.Chat.ChatMessage.AnnouncesShareId. Not a foreign key: the four kinds of share live in
    /// four tables, and which of them this points at is only knowable from the sealed payload.
    /// </summary>
    public Guid? AnnouncesShareId { get; set; }
}

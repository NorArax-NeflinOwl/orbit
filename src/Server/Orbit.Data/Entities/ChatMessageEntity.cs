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
}

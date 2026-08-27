namespace Orbit.Contracts.Chat;

public sealed record ChatMessageDto(
    Guid Id, Guid SenderUserId, Guid RecipientUserId, string CiphertextBase64, string NonceBase64, DateTimeOffset SentAtUtc,
    bool IsEdited, DateTimeOffset? EditedAtUtc,
    /// <summary>
    /// Shared by every copy of one group message, null for an ordinary one-to-one one. The reader is
    /// handed a single copy (see GetGroupConversationQueryHandler); editing or deleting has to name the
    /// whole message, which is what this identifies.
    /// </summary>
    Guid? GroupMessageId = null,
    /// <summary>
    /// Whether every other member of the group has read this - null unless the reader sent it, and null
    /// for a one-to-one message, which reports its read state through the read-receipt endpoint instead.
    /// Two ticks are drawn from this: one member still behind and it is not "read" yet.
    /// </summary>
    bool? ReadByEveryone = null);

/// <summary>
/// What became of one group message for one member. Delivered means the message reached the server
/// addressed to them, which is what a stored copy is; a member who joined afterwards has no copy and so
/// appears in no receipt at all.
/// </summary>
public sealed record GroupMessageReceiptDto(Guid RecipientUserId, DateTimeOffset? ReadAtUtc);

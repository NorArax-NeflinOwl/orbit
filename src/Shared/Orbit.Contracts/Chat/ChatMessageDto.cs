namespace Orbit.Contracts.Chat;

public sealed record ChatMessageDto(
    Guid Id, Guid SenderUserId, Guid RecipientUserId, string CiphertextBase64, string NonceBase64, DateTimeOffset SentAtUtc,
    bool IsEdited, DateTimeOffset? EditedAtUtc,
    /// <summary>
    /// Shared by every copy of one group message, null for an ordinary one-to-one one. The reader is
    /// handed a single copy (see GetGroupConversationQueryHandler); editing or deleting has to name the
    /// whole message, which is what this identifies.
    /// </summary>
    Guid? GroupMessageId = null);

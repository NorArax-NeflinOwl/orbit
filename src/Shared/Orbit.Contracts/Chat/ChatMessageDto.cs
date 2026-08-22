namespace Orbit.Contracts.Chat;

public sealed record ChatMessageDto(
    Guid Id, Guid SenderUserId, Guid RecipientUserId, string CiphertextBase64, string NonceBase64, DateTimeOffset SentAtUtc,
    bool IsEdited, DateTimeOffset? EditedAtUtc);

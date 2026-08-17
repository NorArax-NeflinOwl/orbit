namespace Orbit.Contracts.Chat;

public sealed record SendMessageRequest(Guid RecipientUserId, string CiphertextBase64, string NonceBase64);

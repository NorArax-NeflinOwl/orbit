namespace Orbit.Core.Chat;

/// <summary>
/// One end-to-end-encrypted chat message. Orbit.Api only ever stores and relays CiphertextBase64/
/// NonceBase64 - decryption happens exclusively in the browser holding the matching private key (see
/// wwwroot/js/e2eeChat.js), so the server itself can never read a message's content.
/// </summary>
public sealed class ChatMessage
{
    public Guid Id { get; private set; }
    public Guid SenderUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public string CiphertextBase64 { get; private set; }
    public string NonceBase64 { get; private set; }
    public DateTimeOffset SentAtUtc { get; private set; }

    private ChatMessage(
        Guid id, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, DateTimeOffset sentAtUtc)
    {
        Id = id;
        SenderUserId = senderUserId;
        RecipientUserId = recipientUserId;
        CiphertextBase64 = ciphertextBase64;
        NonceBase64 = nonceBase64;
        SentAtUtc = sentAtUtc;
    }

    public static ChatMessage Create(Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64)
        => new(Guid.NewGuid(), senderUserId, recipientUserId, ciphertextBase64, nonceBase64, DateTimeOffset.UtcNow);

    /// <summary>
    /// Rebuilds a message from already-persisted values, bypassing creation rules.
    /// </summary>
    public static ChatMessage FromPersistence(
        Guid id, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, DateTimeOffset sentAtUtc)
        => new(id, senderUserId, recipientUserId, ciphertextBase64, nonceBase64, sentAtUtc);
}

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
    public bool IsEdited { get; private set; }
    public DateTimeOffset? EditedAtUtc { get; private set; }

    private ChatMessage(
        Guid id, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, DateTimeOffset sentAtUtc,
        bool isEdited, DateTimeOffset? editedAtUtc)
    {
        Id = id;
        SenderUserId = senderUserId;
        RecipientUserId = recipientUserId;
        CiphertextBase64 = ciphertextBase64;
        NonceBase64 = nonceBase64;
        SentAtUtc = sentAtUtc;
        IsEdited = isEdited;
        EditedAtUtc = editedAtUtc;
    }

    public static ChatMessage Create(Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64)
        => new(Guid.NewGuid(), senderUserId, recipientUserId, ciphertextBase64, nonceBase64, DateTimeOffset.UtcNow, isEdited: false, editedAtUtc: null);

    /// <summary>
    /// Rebuilds a message from already-persisted values, bypassing creation rules.
    /// </summary>
    public static ChatMessage FromPersistence(
        Guid id, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, DateTimeOffset sentAtUtc,
        bool isEdited, DateTimeOffset? editedAtUtc)
        => new(id, senderUserId, recipientUserId, ciphertextBase64, nonceBase64, sentAtUtc, isEdited, editedAtUtc);

    /// <summary>
    /// Replaces this message's ciphertext with a re-encrypted edit - only the sender is ever allowed to
    /// do this (see EditMessageCommandHandler's authorization check, which runs before this is called).
    /// </summary>
    public void ApplyEdit(string ciphertextBase64, string nonceBase64, DateTimeOffset editedAtUtc)
    {
        CiphertextBase64 = ciphertextBase64;
        NonceBase64 = nonceBase64;
        IsEdited = true;
        EditedAtUtc = editedAtUtc;
    }
}

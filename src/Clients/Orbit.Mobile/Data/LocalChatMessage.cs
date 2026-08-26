namespace Orbit.Mobile.Data;

/// <summary>
/// A message as the phone holds it, so a conversation can be read with no connection (§5.2 of
/// info/orbit-maui-plan.md makes chat history read-only offline rather than unavailable).
///
/// <b>Stored as ciphertext, and decrypted on read.</b> The obvious alternative - caching the plaintext -
/// would make the local database more revealing than the server, which holds only this. That matters
/// here more than it might elsewhere, because the database is deliberately unencrypted for now (§5.1);
/// keeping chat sealed means that open question costs less while it stays open. Decrypting is one ECDH
/// agreement and an AES-GCM open, which is cheap enough to do per screenful.
/// </summary>
public sealed class LocalChatMessage
{
    /// <summary>The server's id. Messages only exist here once the server has accepted them.</summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The other party, whichever direction the message went. One conversation per person, so this is
    /// what a conversation is keyed by rather than sender and recipient separately.
    /// </summary>
    public Guid OtherUserId { get; set; }

    public Guid SenderUserId { get; set; }

    public string CiphertextBase64 { get; set; } = string.Empty;

    public string NonceBase64 { get; set; } = string.Empty;

    public DateTimeOffset SentAtUtc { get; set; }

    public bool IsEdited { get; set; }
}

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
    /// what a one-to-one conversation is keyed by rather than sender and recipient separately. Meaningless
    /// for a group message, where <see cref="GroupId"/> is the conversation and the other party changes
    /// from message to message.
    /// </summary>
    public Guid OtherUserId { get; set; }

    /// <summary>The group this belongs to, null for an ordinary one-to-one message.</summary>
    public Guid? GroupId { get; set; }

    public Guid SenderUserId { get; set; }

    /// <summary>
    /// Who this copy was sealed for. Only a group needs it: a group message is one copy per member (see
    /// ChatMessage.CreateForGroup), and the reader's own copies are sealed against a recipient's key
    /// rather than the sender's, so opening them means knowing which recipient.
    /// </summary>
    public Guid RecipientUserId { get; set; }

    /// <summary>
    /// Shared by every copy of one group message, null for a one-to-one one. The server hands each reader
    /// a single copy per message; this is what identifies the message the copy belongs to.
    /// </summary>
    public Guid? GroupMessageId { get; set; }

    public string CiphertextBase64 { get; set; } = string.Empty;

    public string NonceBase64 { get; set; } = string.Empty;

    public DateTimeOffset SentAtUtc { get; set; }

    public bool IsEdited { get; set; }

    /// <summary>
    /// Whether every other member of the group has read this. Null unless the reader sent it, and null
    /// for a one-to-one message, which reports its read state per conversation instead. One member still
    /// behind and it is not read yet - see ChatMessageDto.ReadByEveryone.
    /// </summary>
    public bool? IsReadByEveryone { get; set; }
}

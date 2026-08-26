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

    /// <summary>The group this message was posted to, or null for an ordinary one-to-one message.</summary>
    public Guid? GroupId { get; private set; }

    /// <summary>
    /// Shared by every copy of one group message. A group message is encrypted separately for each
    /// member - see CreateForGroup - so "the message" is really N rows; this is what ties them together
    /// so deleting it deletes all of them rather than one person's copy.
    /// </summary>
    public Guid? GroupMessageId { get; private set; }

    private ChatMessage(
        Guid id, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, DateTimeOffset sentAtUtc,
        bool isEdited, DateTimeOffset? editedAtUtc, Guid? groupId, Guid? groupMessageId)
    {
        GroupId = groupId;
        GroupMessageId = groupMessageId;
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
        => new(
            Guid.NewGuid(), senderUserId, recipientUserId, ciphertextBase64, nonceBase64, DateTimeOffset.UtcNow,
            isEdited: false, editedAtUtc: null, groupId: null, groupMessageId: null);

    /// <summary>
    /// One recipient's copy of a group message. Groups reuse the pairwise encryption people already
    /// have rather than introducing a group key: the sender encrypts the same text once per member, and
    /// each copy is readable by exactly the two people whose keys made it. That keeps the server unable
    /// to read anything and needs no key distribution or rotation when membership changes - at the cost
    /// of N rows per message, and of a new member being unable to read anything sent before they
    /// joined, since no copy was ever encrypted for them.
    /// </summary>
    /// <summary>
    /// One copy of a group message. <paramref name="sentAtUtc"/> is passed in rather than read here so
    /// every copy of the same message carries the same instant: read per copy, a message fanned out to
    /// five people had five slightly different times, the one shown depended on which copy happened to
    /// be kept, and a cursor could fall between them and hand back part of a message.
    /// </summary>
    public static ChatMessage CreateForGroup(
        Guid groupId, Guid groupMessageId, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64,
        DateTimeOffset sentAtUtc)
        => new(
            Guid.NewGuid(), senderUserId, recipientUserId, ciphertextBase64, nonceBase64, sentAtUtc,
            isEdited: false, editedAtUtc: null, groupId, groupMessageId);

    /// <summary>
    /// Rebuilds a message from already-persisted values, bypassing creation rules.
    /// </summary>
    public static ChatMessage FromPersistence(
        Guid id, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, DateTimeOffset sentAtUtc,
        bool isEdited, DateTimeOffset? editedAtUtc, Guid? groupId = null, Guid? groupMessageId = null)
        => new(id, senderUserId, recipientUserId, ciphertextBase64, nonceBase64, sentAtUtc, isEdited, editedAtUtc, groupId, groupMessageId);

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

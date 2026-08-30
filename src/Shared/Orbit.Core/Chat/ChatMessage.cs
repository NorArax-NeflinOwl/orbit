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

    /// <summary>
    /// True for a copy that was not written when the message was posted, but re-encrypted afterwards for
    /// somebody who joined the group later - see CreateSharedHistoryCopy. What it copies is unchanged:
    /// same sender, same instant, same words. What is different is that nothing was addressed to this
    /// recipient at the time, which is why the original's delivery receipts leave it out.
    /// </summary>
    public bool IsSharedHistory { get; private set; }

    private ChatMessage(
        Guid id, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, DateTimeOffset sentAtUtc,
        bool isEdited, DateTimeOffset? editedAtUtc, Guid? groupId, Guid? groupMessageId, bool isSharedHistory)
    {
        GroupId = groupId;
        GroupMessageId = groupMessageId;
        IsSharedHistory = isSharedHistory;
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
            isEdited: false, editedAtUtc: null, groupId: null, groupMessageId: null, isSharedHistory: false);

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
            isEdited: false, editedAtUtc: null, groupId, groupMessageId, isSharedHistory: false);

    /// <summary>
    /// A copy of an already-posted group message, re-encrypted for somebody who joined afterwards. The
    /// only way a new member can read anything sent before they arrived: no copy was ever made for them,
    /// and the server holds no key it could make one with, so a member who can already read the message
    /// has to re-seal it under the pairwise key they share with the newcomer.
    ///
    /// Everything except the recipient and the ciphertext is taken from <paramref name="original"/>
    /// rather than from whoever is sharing: who wrote it and when are facts about the message, and a
    /// re-share is not the place they get to be restated.
    /// </summary>
    public static ChatMessage CreateSharedHistoryCopy(
        ChatMessage original, Guid recipientUserId, string ciphertextBase64, string nonceBase64)
        => new(
            Guid.NewGuid(), original.SenderUserId, recipientUserId, ciphertextBase64, nonceBase64, original.SentAtUtc,
            original.IsEdited, original.EditedAtUtc, original.GroupId, original.GroupMessageId, isSharedHistory: true);

    /// <summary>
    /// Rebuilds a message from already-persisted values, bypassing creation rules.
    /// </summary>
    public static ChatMessage FromPersistence(
        Guid id, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, DateTimeOffset sentAtUtc,
        bool isEdited, DateTimeOffset? editedAtUtc, Guid? groupId = null, Guid? groupMessageId = null,
        bool isSharedHistory = false)
        => new(
            id, senderUserId, recipientUserId, ciphertextBase64, nonceBase64, sentAtUtc, isEdited, editedAtUtc, groupId,
            groupMessageId, isSharedHistory);

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

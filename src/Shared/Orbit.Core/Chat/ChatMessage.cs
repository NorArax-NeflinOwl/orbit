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

    /// <summary>
    /// When this message was deleted, or null for one still standing. A deleted message keeps its row
    /// and loses its words: <see cref="CiphertextBase64"/> and <see cref="NonceBase64"/> are emptied, so
    /// "deleted" is true of the stored bytes rather than only of what is drawn.
    ///
    /// Kept rather than removed so the conversation can say a message was here and is not any more.
    /// Deleting the row outright left a hole - the other person's screen simply had one fewer line than
    /// a moment ago, with nothing saying why, which reads as a bug or as never having been sent.
    /// </summary>
    public DateTimeOffset? DeletedAtUtc { get; private set; }

    /// <summary>
    /// Who deleted it. Not always the sender: an admin may delete anybody's message in a group (see
    /// ChatGroup.CanDeleteMessageFrom), and "the sender deleted this" would then be untrue. The clients
    /// name them from the roster they already hold.
    /// </summary>
    public Guid? DeletedByUserId { get; private set; }

    /// <summary>
    /// The share this message is the invitation to, or null for anything else. Written by the sender,
    /// because nobody else can know: the message is sealed and the share id sits inside the sealed
    /// payload - see SendMessageCommand.AnnouncesShareId.
    ///
    /// What it is for is the other direction. When the owner takes the share back, the invitation is
    /// still sitting in the conversation with an "Accept" on it that now leads nowhere, and this is the
    /// only way the server can find it.
    /// </summary>
    public Guid? AnnouncesShareId { get; private set; }

    /// <summary>Whether this message has been deleted - see <see cref="DeletedAtUtc"/>.</summary>
    public bool IsDeleted => DeletedAtUtc is not null;

    private ChatMessage(
        Guid id, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, DateTimeOffset sentAtUtc,
        bool isEdited, DateTimeOffset? editedAtUtc, Guid? groupId, Guid? groupMessageId, bool isSharedHistory,
        Guid? announcesShareId)
    {
        AnnouncesShareId = announcesShareId;
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

    public static ChatMessage Create(
        Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, Guid? announcesShareId = null)
        => new(
            Guid.NewGuid(), senderUserId, recipientUserId, ciphertextBase64, nonceBase64, DateTimeOffset.UtcNow,
            isEdited: false, editedAtUtc: null, groupId: null, groupMessageId: null, isSharedHistory: false, announcesShareId);

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
            isEdited: false, editedAtUtc: null, groupId, groupMessageId, isSharedHistory: false, announcesShareId: null);

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
            original.IsEdited, original.EditedAtUtc, original.GroupId, original.GroupMessageId, isSharedHistory: true,
            original.AnnouncesShareId);

    /// <summary>
    /// Rebuilds a message from already-persisted values, bypassing creation rules.
    /// </summary>
    public static ChatMessage FromPersistence(
        Guid id, Guid senderUserId, Guid recipientUserId, string ciphertextBase64, string nonceBase64, DateTimeOffset sentAtUtc,
        bool isEdited, DateTimeOffset? editedAtUtc, Guid? groupId = null, Guid? groupMessageId = null,
        bool isSharedHistory = false, DateTimeOffset? deletedAtUtc = null, Guid? deletedByUserId = null,
        Guid? announcesShareId = null)
    {
        var message = new ChatMessage(
            id, senderUserId, recipientUserId, ciphertextBase64, nonceBase64, sentAtUtc, isEdited, editedAtUtc, groupId,
            groupMessageId, isSharedHistory, announcesShareId);
        message.DeletedAtUtc = deletedAtUtc;
        message.DeletedByUserId = deletedByUserId;
        return message;
    }

    /// <summary>
    /// Takes the message back: the words go and the row stays, so the conversation can say something was
    /// here. Emptying the ciphertext is what makes this a deletion rather than a flag - a message nobody
    /// can decrypt is deleted whatever any client chooses to draw.
    ///
    /// Says whether anything changed, so deleting the same message twice is harmless and announces
    /// nothing the second time.
    /// </summary>
    public bool Delete(Guid deletedByUserId, DateTimeOffset deletedAtUtc)
    {
        if (IsDeleted)
        {
            return false;
        }

        CiphertextBase64 = string.Empty;
        NonceBase64 = string.Empty;
        DeletedAtUtc = deletedAtUtc;
        DeletedByUserId = deletedByUserId;
        return true;
    }

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

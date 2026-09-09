namespace Orbit.Core.Chat;

public interface IChatMessageRepository
{
    /// <summary>
    /// Both directions between the two users, ordered oldest-first. When sinceUtc is given, only
    /// messages strictly after it are returned - used for polling incremental updates instead of
    /// re-fetching (and re-decrypting) the whole conversation every few seconds.
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> GetConversationAsync(
        Guid userId, Guid otherUserId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken);

    Task AddAsync(ChatMessage message, CancellationToken cancellationToken);

    /// <summary>
    /// Every copy of a group's messages that userId can actually decrypt - the ones addressed to them
    /// and the ones they sent - oldest first. See GetGroupConversationQueryHandler for why the rest are
    /// left out.
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> GetGroupConversationAsync(
        Guid groupId, Guid userId, DateTimeOffset? sinceUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Takes one message's words away and leaves its row, so the conversation can say something was
    /// here - see ChatMessage.Delete. No-op when the message no longer exists. Named for what it does
    /// rather than "Delete": the row survives, and a caller expecting the row to go would be wrong.
    /// </summary>
    Task MarkDeletedAsync(Guid messageId, Guid deletedByUserId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// The same for every message that announced one share - the invitation the owner has just taken
    /// back, which would otherwise sit in the conversation offering an "Accept" that leads nowhere.
    /// Usually one message; more when the same share was offered again as a reminder, which reuses the
    /// share rather than making a second one.
    ///
    /// Answers everybody who was in one of those conversations, so the withdrawal can be announced to
    /// the screens showing it. The caller has only the share id: which two people it reached is a fact
    /// about the messages, not about the command.
    /// </summary>
    Task<IReadOnlyList<Guid>> MarkShareAnnouncementsDeletedAsync(
        Guid shareId, Guid deletedByUserId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken);

    /// <summary>The same for every per-recipient copy of one group posting - see ChatMessage.GroupMessageId.</summary>
    Task MarkGroupMessageDeletedAsync(
        Guid groupMessageId, Guid deletedByUserId, DateTimeOffset deletedAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Removes the copies of a group's messages that are addressed to one member - what that member
    /// takes with them when they leave. Only their own copies: a group message is encrypted separately
    /// for each member, so this takes nothing away from anybody else. Copies that member sent to others
    /// stay, because those are the others' to read.
    /// </summary>
    Task DeleteGroupCopiesForAsync(Guid groupId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Looks up a single message by id, or null if it doesn't exist - used by EditMessageCommandHandler to check who sent it before allowing an edit.</summary>
    Task<ChatMessage?> GetByIdAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>Overwrites an existing message's ciphertext/nonce and marks it edited. Caller (EditMessageCommandHandler) is responsible for the sender-owns-this-message check.</summary>
    Task UpdateContentAsync(Guid messageId, string ciphertextBase64, string nonceBase64, DateTimeOffset editedAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Every stored copy of one group message - what a group message really is (see
    /// ChatMessage.GroupMessageId). Editing has to reach all of them, since each is separately
    /// encrypted and leaving one behind would show different members different words.
    /// </summary>
    Task<IReadOnlyList<ChatMessage>> GetGroupMessageCopiesAsync(Guid groupMessageId, CancellationToken cancellationToken);

    /// <summary>
    /// Marks every not-yet-read message that otherUserId sent to readerUserId as read as of readAtUtc.
    /// A no-op for messages already marked read, so it's safe to call on every poll tick rather than
    /// only once.
    /// </summary>
    /// <summary>
    /// Marks everything the other party sent this reader as read, and answers whether anything actually
    /// changed. The answer matters: telling the other side about a read that did not happen is what
    /// makes two open windows announce at each other for as long as they are both open - see
    /// MarkConversationAsReadCommandHandler.
    /// </summary>
    Task<bool> MarkConversationAsReadAsync(Guid readerUserId, Guid otherUserId, DateTimeOffset readAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// The latest SentAtUtc among senderUserId's messages to recipientUserId that recipientUserId has
    /// already read, or null if none have been read yet - lets the sender's UI show a single vs. double
    /// checkmark without transferring per-message read state.
    /// </summary>
    Task<DateTimeOffset?> GetReadUpToUtcAsync(Guid senderUserId, Guid recipientUserId, CancellationToken cancellationToken);
    /// <summary>
    /// How many messages each sender has waiting unread for this reader, keyed by sender - one-to-one
    /// conversations only. Answered in a single query rather than per contact, because the chat list
    /// asks for all of them on every poll tick.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> GetUnreadCountsBySenderAsync(Guid readerUserId, CancellationToken cancellationToken);
    /// <summary>
    /// Marks every copy addressed to readerUserId in this group as read, and answers whether anything
    /// actually changed. The group counterpart of <see cref="MarkConversationAsReadAsync"/>, and a no-op
    /// for copies already marked, so it is safe to call on every poll tick rather than only once - the
    /// answer is what keeps a no-op from being announced to the rest of the group as news.
    /// </summary>
    Task<bool> MarkGroupConversationAsReadAsync(
        Guid readerUserId, Guid groupId, DateTimeOffset readAtUtc, CancellationToken cancellationToken);

    /// <summary>
    /// Who each of these group messages reached and which of them have read it, keyed by GroupMessageId.
    /// Asked for a whole page of messages at once, because the conversation needs it for every message
    /// it draws.
    ///
    /// Copies re-encrypted after the fact for a later joiner (ChatMessage.IsSharedHistory) are left out
    /// by every implementation: a receipt says whether a message reached the people it was posted to,
    /// and a copy made afterwards was not one of those deliveries. Counting them would take a sender's
    /// ticks away for a delivery that had already happened.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<GroupMessageReceipt>>> GetGroupReceiptsAsync(
        IReadOnlyCollection<Guid> groupMessageIds, CancellationToken cancellationToken);
}
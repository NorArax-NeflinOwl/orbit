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
    Task<IReadOnlyList<ChatMessage>> GetGroupConversationAsync(Guid groupId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Removes one message row. No-op when it no longer exists.</summary>
    Task DeleteAsync(Guid messageId, CancellationToken cancellationToken);

    /// <summary>Removes every per-recipient copy of one group posting - see ChatMessage.GroupMessageId.</summary>
    Task DeleteGroupMessageAsync(Guid groupMessageId, CancellationToken cancellationToken);

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
    Task MarkConversationAsReadAsync(Guid readerUserId, Guid otherUserId, DateTimeOffset readAtUtc, CancellationToken cancellationToken);

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
}
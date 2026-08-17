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
}

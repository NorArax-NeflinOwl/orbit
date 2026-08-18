namespace Orbit.Core.Chat;

public interface IChatConversationAccessRepository
{
    /// <summary>
    /// Looks up the access state for the conversation between these two users, regardless of which one
    /// is passed first - null means they have never exchanged a message, so nothing is gated yet.
    /// </summary>
    Task<ChatConversationAccess?> GetAsync(Guid userId, Guid otherUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates the row the moment two users' very first message is sent, with initiatedByUserId as the
    /// party who does not need approval - a no-op if a row for this pair already exists, since only the
    /// first message should ever decide who the initiator was.
    /// </summary>
    Task EnsureCreatedAsync(Guid initiatedByUserId, Guid otherUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Approves the conversation on approvingUserId's behalf. Returns false (a no-op) if no row exists
    /// yet, or if approvingUserId is actually the one who started it - there is nothing for either of
    /// those to approve.
    /// </summary>
    Task<bool> ApproveAsync(Guid approvingUserId, Guid otherUserId, CancellationToken cancellationToken);
}

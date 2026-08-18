namespace Orbit.Core.Chat;

/// <summary>
/// Gates a brand-new conversation the way Instagram's message requests do: the first message ever sent
/// between two users creates this in a pending state (see SendMessageCommandHandler), and OtherUserId -
/// whichever of the two did NOT send that first message - has to explicitly approve it (see
/// ApproveConversationCommand) before their own replies go through. The initiator can keep sending in
/// the meantime; only the other party's outgoing messages are blocked until they approve.
/// </summary>
public sealed class ChatConversationAccess
{
    public Guid Id { get; private set; }
    public Guid InitiatedByUserId { get; private set; }
    public Guid OtherUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? ApprovedAtUtc { get; private set; }

    public bool IsApproved => ApprovedAtUtc is not null;

    private ChatConversationAccess(
        Guid id, Guid initiatedByUserId, Guid otherUserId, DateTimeOffset createdAtUtc, DateTimeOffset? approvedAtUtc)
    {
        Id = id;
        InitiatedByUserId = initiatedByUserId;
        OtherUserId = otherUserId;
        CreatedAtUtc = createdAtUtc;
        ApprovedAtUtc = approvedAtUtc;
    }

    public static ChatConversationAccess Create(Guid initiatedByUserId, Guid otherUserId)
        => new(Guid.NewGuid(), initiatedByUserId, otherUserId, DateTimeOffset.UtcNow, approvedAtUtc: null);

    /// <summary>
    /// Rebuilds an access row from already-persisted values, bypassing creation rules.
    /// </summary>
    public static ChatConversationAccess FromPersistence(
        Guid id, Guid initiatedByUserId, Guid otherUserId, DateTimeOffset createdAtUtc, DateTimeOffset? approvedAtUtc)
        => new(id, initiatedByUserId, otherUserId, createdAtUtc, approvedAtUtc);

    /// <summary>Whoever started the conversation can always send; anyone else needs it approved first.</summary>
    public bool CanSend(Guid userId) => userId == InitiatedByUserId || IsApproved;

    /// <summary>No-op if already approved, so approving twice (e.g. a duplicate click) never resets ApprovedAtUtc.</summary>
    public void Approve()
    {
        if (IsApproved)
        {
            return;
        }

        ApprovedAtUtc = DateTimeOffset.UtcNow;
    }
}

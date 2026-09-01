namespace Orbit.Core.Chat;

/// <summary>
/// One direction of a chat relationship: "ContactUserId shows up in OwnerUserId's chat list". Created
/// (both directions at once) the moment either side sends the first message between them - see
/// SendMessageCommandHandler.
/// </summary>
public sealed class Contact
{
    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid ContactUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset LastMessageAtUtc { get; private set; }

    /// <summary>
    /// Whether this conversation has been put away by the person whose list it is on. One direction
    /// only: a Contact row already belongs to one owner, so archiving is theirs and says nothing to
    /// the other party - who has their own row and their own answer.
    /// </summary>
    public bool IsArchived { get; private set; }

    private Contact(Guid id, Guid ownerUserId, Guid contactUserId, DateTimeOffset createdAtUtc, DateTimeOffset lastMessageAtUtc)
    {
        Id = id;
        OwnerUserId = ownerUserId;
        ContactUserId = contactUserId;
        CreatedAtUtc = createdAtUtc;
        LastMessageAtUtc = lastMessageAtUtc;
    }

    public static Contact Create(Guid ownerUserId, Guid contactUserId, DateTimeOffset lastMessageAtUtc)
        => new(Guid.NewGuid(), ownerUserId, contactUserId, lastMessageAtUtc, lastMessageAtUtc);

    /// <summary>
    /// Rebuilds a contact from already-persisted values, bypassing creation rules.
    /// </summary>
    public static Contact FromPersistence(
        Guid id, Guid ownerUserId, Guid contactUserId, DateTimeOffset createdAtUtc, DateTimeOffset lastMessageAtUtc,
        bool isArchived = false)
        => new(id, ownerUserId, contactUserId, createdAtUtc, lastMessageAtUtc) { IsArchived = isArchived };

    public void UpdateLastMessageAt(DateTimeOffset lastMessageAtUtc)
    {
        LastMessageAtUtc = lastMessageAtUtc;
    }

    /// <summary>
    /// Puts the conversation away, or brings it back. Archiving is not deleting: every message stays,
    /// and the row moves to a list of its own rather than off the account.
    /// </summary>
    public void SetArchived(bool isArchived) => IsArchived = isArchived;
}

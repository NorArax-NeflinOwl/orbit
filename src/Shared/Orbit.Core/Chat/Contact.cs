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

    /// <summary>
    /// When this reader last cleared the conversation, if they ever did. Everything sent at or before
    /// it is left out of what they are shown.
    ///
    /// A line rather than a delete, because a one-to-one message is one row that both people read:
    /// deleting it would reach into somebody else's conversation and take words out of it. Clearing is
    /// the same kind of fact archiving is - one side's own view of their own list - and the other party
    /// keeps everything, exactly as they had it.
    /// </summary>
    public DateTimeOffset? HistoryClearedAtUtc { get; private set; }

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
        bool isArchived = false, DateTimeOffset? historyClearedAtUtc = null)
        => new(id, ownerUserId, contactUserId, createdAtUtc, lastMessageAtUtc)
        {
            IsArchived = isArchived,
            HistoryClearedAtUtc = historyClearedAtUtc
        };

    public void UpdateLastMessageAt(DateTimeOffset lastMessageAtUtc)
    {
        LastMessageAtUtc = lastMessageAtUtc;
    }

    /// <summary>
    /// Puts the conversation away, or brings it back. Archiving is not deleting: every message stays,
    /// and the row moves to a list of its own rather than off the account.
    /// </summary>
    public void SetArchived(bool isArchived) => IsArchived = isArchived;

    /// <summary>
    /// Empties the conversation as this reader sees it, from now backwards. Moving the line forward
    /// only: clearing twice cannot uncover what the first clearing hid, and a clock that has drifted
    /// backwards cannot either.
    /// </summary>
    public void ClearHistory(DateTimeOffset clearedAtUtc)
    {
        if (HistoryClearedAtUtc is null || clearedAtUtc > HistoryClearedAtUtc)
        {
            HistoryClearedAtUtc = clearedAtUtc;
        }
    }
}

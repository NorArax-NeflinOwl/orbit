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
        Guid id, Guid ownerUserId, Guid contactUserId, DateTimeOffset createdAtUtc, DateTimeOffset lastMessageAtUtc)
        => new(id, ownerUserId, contactUserId, createdAtUtc, lastMessageAtUtc);

    public void UpdateLastMessageAt(DateTimeOffset lastMessageAtUtc)
    {
        LastMessageAtUtc = lastMessageAtUtc;
    }
}

using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory;

/// <summary>
/// A grant of access to SourceWarehouseId - created via ShareWarehouseCommand as a pending offer, and
/// activated once the recipient accepts it from the chat message carrying this share's id (see
/// AcceptWarehouseShareCommand). Accepting never copies anything: OwnerUserId names the warehouse's one
/// permanent owner, and an accepted row *is* the recipient's access to that same warehouse and every
/// item in it. Direct mirror of NoteShare - see its class comment.
/// </summary>
public sealed class WarehouseShare
{
    public Guid Id { get; private set; }
    public Guid SourceWarehouseId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public ShareAccessLevel AccessLevel { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public bool IsAccepted => AcceptedAtUtc is not null;

    private WarehouseShare(
        Guid id, Guid sourceWarehouseId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc)
    {
        Id = id;
        SourceWarehouseId = sourceWarehouseId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public static WarehouseShare Create(
        Guid sourceWarehouseId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel = ShareAccessLevel.ReadOnly)
        => new(Guid.NewGuid(), sourceWarehouseId, ownerUserId, recipientUserId, accessLevel, DateTimeOffset.UtcNow, acceptedAtUtc: null);

    /// <summary>Rebuilds a share from already-persisted values, bypassing creation rules.</summary>
    public static WarehouseShare FromPersistence(
        Guid id, Guid sourceWarehouseId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc)
        => new(id, sourceWarehouseId, ownerUserId, recipientUserId, accessLevel, createdAtUtc, acceptedAtUtc);

    /// <summary>No-op if already accepted, so accepting the same share twice (e.g. a duplicate click) is harmless.</summary>
    public void MarkAccepted()
    {
        AcceptedAtUtc ??= DateTimeOffset.UtcNow;
    }
}

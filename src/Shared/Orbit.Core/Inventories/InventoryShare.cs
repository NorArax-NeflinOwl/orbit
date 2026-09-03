using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories;

/// <summary>
/// A grant of access to SourceInventoryId - created via ShareInventoryCommand as a pending offer, and
/// activated once the recipient accepts it from the chat message carrying this share's id (see
/// AcceptInventoryShareCommand). Accepting never copies anything: OwnerUserId names the inventory's one
/// permanent owner, and an accepted row *is* the recipient's access to that same inventory and every
/// item in it. Direct mirror of NoteShare - see its class comment.
/// </summary>
public sealed class InventoryShare
{
    public Guid Id { get; private set; }
    public Guid SourceInventoryId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public ShareAccessLevel AccessLevel { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public bool IsAccepted => AcceptedAtUtc is not null;

    private InventoryShare(
        Guid id, Guid sourceInventoryId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc)
    {
        Id = id;
        SourceInventoryId = sourceInventoryId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public static InventoryShare Create(
        Guid sourceInventoryId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel = ShareAccessLevel.ReadOnly)
        => new(Guid.NewGuid(), sourceInventoryId, ownerUserId, recipientUserId, accessLevel, DateTimeOffset.UtcNow, acceptedAtUtc: null);

    /// <summary>Rebuilds a share from already-persisted values, bypassing creation rules.</summary>
    public static InventoryShare FromPersistence(
        Guid id, Guid sourceInventoryId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc)
        => new(id, sourceInventoryId, ownerUserId, recipientUserId, accessLevel, createdAtUtc, acceptedAtUtc);

    /// <summary>No-op if already accepted, so accepting the same share twice (e.g. a duplicate click) is harmless.</summary>
    public void MarkAccepted()
    {
        AcceptedAtUtc ??= DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Raises what this share grants, and only ever raises it - answering a request for edit access is
    /// the point, and an owner re-sharing at a lower level than they already gave is far more likely to
    /// be a stale form than an intention to take access away. Returns whether anything changed.
    /// </summary>
    public bool RaiseAccessLevelTo(ShareAccessLevel accessLevel)
    {
        if (accessLevel <= AccessLevel)
        {
            return false;
        }

        AccessLevel = accessLevel;
        return true;
    }

}

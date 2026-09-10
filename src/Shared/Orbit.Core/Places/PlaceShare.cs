using Orbit.Core.Abstractions;

namespace Orbit.Core.Places;

/// <summary>
/// A grant of access to <see cref="SourcePlaceId"/> - made by SharePlaceCommand as a pending offer and
/// taken up from the chat message that carries this share's id (see AcceptPlaceShareCommand).
///
/// The same shape as NoteShare, and deliberately so: accepting does not copy the place, so
/// <see cref="OwnerUserId"/> names its one permanent owner and this row *is* the recipient's access to
/// that same row - see PlaceAccessResolver, which reads <see cref="AccessLevel"/> back out on every load.
///
/// A place is what the "share a single entry" answer turned out to need. It is small - a name, a point
/// and three answers about how it is drawn - so there is nothing here about pinning or about locks: a
/// place has no editor to hold open and no list of its own to sit at the top of.
/// </summary>
public sealed class PlaceShare
{
    public Guid Id { get; private set; }
    public Guid SourcePlaceId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public ShareAccessLevel AccessLevel { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public bool IsAccepted => AcceptedAtUtc is not null;

    private PlaceShare(
        Guid id, Guid sourcePlaceId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc)
    {
        Id = id;
        SourcePlaceId = sourcePlaceId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public static PlaceShare Create(
        Guid sourcePlaceId, Guid ownerUserId, Guid recipientUserId,
        ShareAccessLevel accessLevel = ShareAccessLevel.ReadOnly)
        => new(
            Guid.NewGuid(), sourcePlaceId, ownerUserId, recipientUserId, accessLevel, DateTimeOffset.UtcNow,
            acceptedAtUtc: null);

    /// <summary>Rebuilds one from a stored row, with none of the creation rules.</summary>
    public static PlaceShare FromPersistence(
        Guid id, Guid sourcePlaceId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc)
        => new(id, sourcePlaceId, ownerUserId, recipientUserId, accessLevel, createdAtUtc, acceptedAtUtc);

    /// <summary>No-op when it is already accepted, so a second press of Accept is harmless.</summary>
    public void MarkAccepted() => AcceptedAtUtc ??= DateTimeOffset.UtcNow;

    /// <summary>
    /// Raises what this grant gives, and only ever raises it - answering a request for edit access is
    /// the point, and re-sharing at a lower level is far more likely to be a stale form than an
    /// intention to take access away. Says whether anything changed.
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

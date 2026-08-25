using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar;

/// <summary>
/// A grant of access to SourceCalendarEventId - mirrors Orbit.Core.Notes.NoteShare, see its class
/// comment for why accepting no longer copies the event. Created when the event's owner adds a contact
/// as a guest (see ShareCalendarEventCommand), and activated once the recipient accepts it from the chat
/// message that carries this share's id (see AcceptCalendarEventShareCommand).
/// </summary>
public sealed class CalendarEventShare
{
    public Guid Id { get; private set; }
    public Guid SourceCalendarEventId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public ShareAccessLevel AccessLevel { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    public bool IsAccepted => AcceptedAtUtc is not null;

    private CalendarEventShare(
        Guid id, Guid sourceCalendarEventId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc)
    {
        Id = id;
        SourceCalendarEventId = sourceCalendarEventId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
    }

    public static CalendarEventShare Create(
        Guid sourceCalendarEventId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel = ShareAccessLevel.ReadOnly)
        => new(Guid.NewGuid(), sourceCalendarEventId, ownerUserId, recipientUserId, accessLevel, DateTimeOffset.UtcNow, acceptedAtUtc: null);

    /// <summary>Rebuilds a share from already-persisted values, bypassing creation rules.</summary>
    public static CalendarEventShare FromPersistence(
        Guid id, Guid sourceCalendarEventId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc)
        => new(id, sourceCalendarEventId, ownerUserId, recipientUserId, accessLevel, createdAtUtc, acceptedAtUtc);

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

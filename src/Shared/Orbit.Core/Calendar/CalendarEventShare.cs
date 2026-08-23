using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar;

/// <summary>
/// An offer to add a copy of a calendar event to another user's own calendar - created when the event's
/// owner adds a contact as a guest (see ShareCalendarEventCommand), and resolved once the recipient
/// accepts it from the chat message that carries this share's id (see AcceptCalendarEventShareCommand).
/// SourceCalendarEventId always belongs to OwnerUserId.
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

    /// <summary>The copy created in the recipient's own calendar - set once accepted, null until then.</summary>
    public Guid? SharedCalendarEventId { get; private set; }

    /// <summary>
    /// The id of the user who first created the event being offered, before any sharing - mirrors
    /// <see cref="Orbit.Core.Notes.NoteShare.OriginalOwnerUserId"/>, see its comment.
    /// </summary>
    public Guid OriginalOwnerUserId { get; private set; }

    public bool IsAccepted => AcceptedAtUtc is not null;

    private CalendarEventShare(
        Guid id, Guid sourceCalendarEventId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, Guid? sharedCalendarEventId, Guid originalOwnerUserId)
    {
        Id = id;
        SourceCalendarEventId = sourceCalendarEventId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
        SharedCalendarEventId = sharedCalendarEventId;
        OriginalOwnerUserId = originalOwnerUserId;
    }

    public static CalendarEventShare Create(
        Guid sourceCalendarEventId, Guid ownerUserId, Guid recipientUserId, Guid originalOwnerUserId, ShareAccessLevel accessLevel = ShareAccessLevel.ReadOnly)
        => new(
            Guid.NewGuid(), sourceCalendarEventId, ownerUserId, recipientUserId, accessLevel, DateTimeOffset.UtcNow,
            acceptedAtUtc: null, sharedCalendarEventId: null, originalOwnerUserId);

    /// <summary>
    /// Rebuilds a share from already-persisted values, bypassing creation rules.
    /// </summary>
    public static CalendarEventShare FromPersistence(
        Guid id, Guid sourceCalendarEventId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, Guid? sharedCalendarEventId, Guid originalOwnerUserId)
        => new(id, sourceCalendarEventId, ownerUserId, recipientUserId, accessLevel, createdAtUtc, acceptedAtUtc, sharedCalendarEventId, originalOwnerUserId);

    /// <summary>
    /// No-op if already accepted, so accepting the same share twice (e.g. a duplicate click) never
    /// creates a second calendar copy.
    /// </summary>
    public void MarkAccepted(Guid sharedCalendarEventId)
    {
        if (IsAccepted)
        {
            return;
        }

        AcceptedAtUtc = DateTimeOffset.UtcNow;
        SharedCalendarEventId = sharedCalendarEventId;
    }
}

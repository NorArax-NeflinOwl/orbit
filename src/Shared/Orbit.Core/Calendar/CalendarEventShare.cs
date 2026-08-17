namespace Orbit.Core.Calendar;

/// <summary>
/// An offer to add a read-only copy of a calendar event to another user's own calendar - created when
/// the event's owner adds a contact as a guest (see ShareCalendarEventCommand), and resolved once the
/// recipient accepts it from the chat message that carries this share's id (see
/// AcceptCalendarEventShareCommand). SourceCalendarEventId always belongs to OwnerUserId.
/// </summary>
public sealed class CalendarEventShare
{
    public Guid Id { get; private set; }
    public Guid SourceCalendarEventId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    /// <summary>The read-only copy created in the recipient's own calendar - set once accepted, null until then.</summary>
    public Guid? SharedCalendarEventId { get; private set; }

    public bool IsAccepted => AcceptedAtUtc is not null;

    private CalendarEventShare(
        Guid id, Guid sourceCalendarEventId, Guid ownerUserId, Guid recipientUserId, DateTimeOffset createdAtUtc,
        DateTimeOffset? acceptedAtUtc, Guid? sharedCalendarEventId)
    {
        Id = id;
        SourceCalendarEventId = sourceCalendarEventId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
        SharedCalendarEventId = sharedCalendarEventId;
    }

    public static CalendarEventShare Create(Guid sourceCalendarEventId, Guid ownerUserId, Guid recipientUserId)
        => new(Guid.NewGuid(), sourceCalendarEventId, ownerUserId, recipientUserId, DateTimeOffset.UtcNow, acceptedAtUtc: null, sharedCalendarEventId: null);

    /// <summary>
    /// Rebuilds a share from already-persisted values, bypassing creation rules.
    /// </summary>
    public static CalendarEventShare FromPersistence(
        Guid id, Guid sourceCalendarEventId, Guid ownerUserId, Guid recipientUserId, DateTimeOffset createdAtUtc,
        DateTimeOffset? acceptedAtUtc, Guid? sharedCalendarEventId)
        => new(id, sourceCalendarEventId, ownerUserId, recipientUserId, createdAtUtc, acceptedAtUtc, sharedCalendarEventId);

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

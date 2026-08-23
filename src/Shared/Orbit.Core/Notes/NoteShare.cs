using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes;

/// <summary>
/// An offer to add a copy of a note to another user's own notes - created via ShareNoteCommand, and
/// resolved once the recipient accepts it from the chat message that carries this share's id (see
/// AcceptNoteShareCommand). Mirrors Orbit.Core.Calendar.CalendarEventShare - see its class comment for
/// the reasoning behind this shape. SourceNoteId always belongs to OwnerUserId.
/// </summary>
public sealed class NoteShare
{
    public Guid Id { get; private set; }
    public Guid SourceNoteId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public ShareAccessLevel AccessLevel { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    /// <summary>The copy created in the recipient's own notes - set once accepted, null until then.</summary>
    public Guid? SharedNoteId { get; private set; }

    /// <summary>
    /// The id of the user who first created the note being offered, before any sharing - computed once
    /// by ShareNoteCommandHandler at offer time (from the source note's own
    /// <see cref="Note.EffectiveOwnerUserId"/>) and carried onto the recipient's copy once accepted, see
    /// <see cref="Note.OriginalOwnerUserId"/>.
    /// </summary>
    public Guid OriginalOwnerUserId { get; private set; }

    public bool IsAccepted => AcceptedAtUtc is not null;

    private NoteShare(
        Guid id, Guid sourceNoteId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, Guid? sharedNoteId, Guid originalOwnerUserId)
    {
        Id = id;
        SourceNoteId = sourceNoteId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
        SharedNoteId = sharedNoteId;
        OriginalOwnerUserId = originalOwnerUserId;
    }

    public static NoteShare Create(
        Guid sourceNoteId, Guid ownerUserId, Guid recipientUserId, Guid originalOwnerUserId, ShareAccessLevel accessLevel = ShareAccessLevel.ReadOnly)
        => new(Guid.NewGuid(), sourceNoteId, ownerUserId, recipientUserId, accessLevel, DateTimeOffset.UtcNow,
            acceptedAtUtc: null, sharedNoteId: null, originalOwnerUserId);

    /// <summary>
    /// Rebuilds a share from already-persisted values, bypassing creation rules.
    /// </summary>
    public static NoteShare FromPersistence(
        Guid id, Guid sourceNoteId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, Guid? sharedNoteId, Guid originalOwnerUserId)
        => new(id, sourceNoteId, ownerUserId, recipientUserId, accessLevel, createdAtUtc, acceptedAtUtc, sharedNoteId, originalOwnerUserId);

    /// <summary>
    /// No-op if already accepted, so accepting the same share twice (e.g. a duplicate click) never
    /// creates a second note copy.
    /// </summary>
    public void MarkAccepted(Guid sharedNoteId)
    {
        if (IsAccepted)
        {
            return;
        }

        AcceptedAtUtc = DateTimeOffset.UtcNow;
        SharedNoteId = sharedNoteId;
    }
}

using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes;

/// <summary>
/// A grant of access to SourceNoteId - created via ShareNoteCommand as a pending offer, and activated
/// once the recipient accepts it from the chat message that carries this share's id (see
/// AcceptNoteShareCommand). Unlike an earlier version of this feature, accepting does not copy the
/// note: OwnerUserId always names the note's one true, permanent owner, and once AcceptedAtUtc is set
/// this row itself *is* the recipient's access to that same row - see NoteAccessResolver, which is what
/// actually reads AccessLevel back out on every load rather than it ever being copied anywhere.
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

    /// <summary>
    /// Whether the *recipient* keeps this note at the top of their own page. The note's own IsPinned
    /// belongs to its owner and says where it sits on theirs, which is a different question with the
    /// same name - so a recipient's answer had nowhere on the server to live and was kept in one
    /// browser's localStorage, where it did not follow them to a second browser or to the phone.
    ///
    /// It lives here because this row *is* the recipient's relationship to that note: one per
    /// recipient, gone when their access is, and never visible to the owner.
    /// </summary>
    public bool IsPinnedByRecipient { get; private set; }

    public bool IsAccepted => AcceptedAtUtc is not null;

    private NoteShare(
        Guid id, Guid sourceNoteId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, bool isPinnedByRecipient)
    {
        Id = id;
        SourceNoteId = sourceNoteId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
        IsPinnedByRecipient = isPinnedByRecipient;
    }

    public static NoteShare Create(
        Guid sourceNoteId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel = ShareAccessLevel.ReadOnly)
        => new(
            Guid.NewGuid(), sourceNoteId, ownerUserId, recipientUserId, accessLevel, DateTimeOffset.UtcNow,
            acceptedAtUtc: null, isPinnedByRecipient: false);

    /// <summary>Rebuilds a share from already-persisted values, bypassing creation rules.</summary>
    public static NoteShare FromPersistence(
        Guid id, Guid sourceNoteId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, bool isPinnedByRecipient = false)
        => new(
            id, sourceNoteId, ownerUserId, recipientUserId, accessLevel, createdAtUtc, acceptedAtUtc,
            isPinnedByRecipient);

    /// <summary>
    /// Where the recipient wants this note on their own page. Says whether anything changed, so a press
    /// that sets it to what it already was writes nothing.
    /// </summary>
    public bool SetPinnedByRecipient(bool isPinned)
    {
        if (IsPinnedByRecipient == isPinned)
        {
            return false;
        }

        IsPinnedByRecipient = isPinned;
        return true;
    }

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

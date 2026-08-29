namespace Orbit.Core.Notes;

public interface INoteShareRepository
{
    Task AddAsync(NoteShare share, CancellationToken cancellationToken);

    /// <summary>
    /// Scoped to recipientUserId, the same way INoteRepository.GetByIdAsync is scoped to an owner -
    /// returns null both when the share doesn't exist and when it exists but was offered to someone
    /// else, so a caller can't tell one case from the other by probing ids.
    /// </summary>
    Task<NoteShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(NoteShare share, CancellationToken cancellationToken);

    /// <summary>
    /// The share already offered for sourceNoteId to recipientUserId, if one exists - accepted or still
    /// pending, either way counts as "already shared" for ShareNoteCommandHandler's duplicate check, so
    /// it re-sends the existing offer as a reminder instead of creating a second one.
    /// </summary>
    Task<NoteShare?> FindExistingAsync(Guid sourceNoteId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>
    /// The *accepted* grant for sourceNoteId to recipientUserId, if one exists - this is what
    /// NoteAccessResolver treats as "recipientUserId currently has access to this note", as opposed to
    /// FindExistingAsync above, which also matches a still-pending offer nobody has accepted yet.
    /// </summary>
    Task<NoteShare?> FindAcceptedGrantAsync(Guid sourceNoteId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Every note recipientUserId has accepted access to, regardless of which owner shared it - see NoteAccessResolver.ResolveAllAsync.</summary>
    Task<IReadOnlyList<NoteShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of ownerUserId's own notes somebody else currently holds accepted access to - the owner's
    /// side of the relationship, which nothing else exposes: IsShared on a DTO says "shared *with* me",
    /// and an owner's copy of a note two people can edit otherwise looks exactly like a private one.
    ///
    /// A whole set in one query rather than a question per note, because the caller asks it of every
    /// note in a list. See NoteAccessResolver and info/orbit-maui-plan.md §5.4, which needs this to
    /// decide what may be edited offline.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetSharedOutNoteIdsAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the accepted grant that puts this note on recipientUserId's list, taking it off their
    /// list without touching the owner's. Scoped to the recipient, so it can only ever remove their own
    /// access. A no-op when there is no such grant.
    /// </summary>
    Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken);
}

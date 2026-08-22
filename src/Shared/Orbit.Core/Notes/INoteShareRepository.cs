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
}

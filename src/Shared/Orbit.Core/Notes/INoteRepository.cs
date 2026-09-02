namespace Orbit.Core.Notes;

public interface INoteRepository
{
    /// <summary>
    /// Everything userId owns, or - when updatedSinceUtc is given - only what changed at or after it.
    /// The cursor is applied in the database: a client catching up asks for a delta, and answering it by
    /// fetching everything and discarding most of it saved the wire and nothing else.
    /// </summary>
    Task<IReadOnlyList<Note>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken);

    Task<Note?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(Note note, CancellationToken cancellationToken);

    Task UpdateAsync(Note note, CancellationToken cancellationToken);
    /// <summary>
    /// Writes only who holds the edit lock and until when. Holding a note open is not a change to it,
    /// and <see cref="UpdateAsync"/> writes the whole row - so a heartbeat every twenty seconds rewrote
    /// the note's entire text to say somebody still had the page open. Mirrors
    /// Orbit.Core.Tasks.ITaskRepository.UpdateLockAsync, which was written after that cost a production
    /// 500 on the task list's own lock.
    /// </summary>
    Task UpdateLockAsync(Note note, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}

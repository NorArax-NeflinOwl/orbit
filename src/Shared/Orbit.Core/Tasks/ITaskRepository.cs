namespace Orbit.Core.Tasks;

public interface ITaskRepository
{
    /// <summary>
    /// Everything userId owns, or - when updatedSinceUtc is given - only what changed at or after it.
    /// The cursor is applied in the database: a client catching up asks for a delta, and answering it by
    /// fetching everything and discarding most of it saved the wire and nothing else.
    /// </summary>
    Task<IReadOnlyList<TaskList>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken);

    Task<TaskList?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Every list of userId's, other than <paramref name="exceptListId"/>, that holds an entry with one
    /// of <paramref name="itemIds"/> - what tells a save that an id it was handed is already taken.
    ///
    /// Clients now mint entry ids themselves, so that an entry written with no connection has one
    /// identity from the moment it exists rather than being renamed by its first successful push. Two
    /// clients can therefore hand over the same id, which is what this exists to notice.
    /// </summary>
    Task<IReadOnlyList<TaskList>> GetHoldingItemsAsync(
        Guid userId, Guid exceptListId, IReadOnlyList<Guid> itemIds, CancellationToken cancellationToken);

    Task AddAsync(TaskList taskList, CancellationToken cancellationToken);

    Task UpdateAsync(TaskList taskList, CancellationToken cancellationToken);

    /// <summary>
    /// Writes only who holds the edit lock and until when. Taking a lock is not an edit of the list, and
    /// <see cref="UpdateAsync"/> replaces every entry on it wholesale - so a heartbeat every twenty
    /// seconds rewrote a whole checklist, and its links and categories with it, to say that somebody
    /// still had the page open. That is where the duplicate-key failures on /lock came from.
    /// </summary>
    Task UpdateLockAsync(TaskList taskList, CancellationToken cancellationToken);

    /// <summary>
    /// Persists every list in one atomic save - needed when a single operation touches more than one
    /// task list (e.g. moving an item out of one list and into another via MoveTaskItemCommandHandler),
    /// so a mid-operation failure can't duplicate or drop the moved item across the two lists.
    /// </summary>
    Task UpdateManyAsync(IReadOnlyList<TaskList> taskLists, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}

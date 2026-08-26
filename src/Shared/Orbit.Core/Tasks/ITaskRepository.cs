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

    Task AddAsync(TaskList taskList, CancellationToken cancellationToken);

    Task UpdateAsync(TaskList taskList, CancellationToken cancellationToken);

    /// <summary>
    /// Persists every list in one atomic save - needed when a single operation touches more than one
    /// task list (e.g. moving an item out of one list and into another via MoveTaskItemCommandHandler),
    /// so a mid-operation failure can't duplicate or drop the moved item across the two lists.
    /// </summary>
    Task UpdateManyAsync(IReadOnlyList<TaskList> taskLists, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}

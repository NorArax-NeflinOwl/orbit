namespace Orbit.Core.Tasks;

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskList>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

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

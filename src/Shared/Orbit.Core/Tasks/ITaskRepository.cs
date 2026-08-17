namespace Orbit.Core.Tasks;

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskList>> GetAllAsync(Guid userId, CancellationToken cancellationToken);

    Task<TaskList?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(TaskList taskList, CancellationToken cancellationToken);

    Task UpdateAsync(TaskList taskList, CancellationToken cancellationToken);

    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);
}

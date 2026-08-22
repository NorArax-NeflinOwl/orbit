namespace Orbit.Core.Tasks;

public interface ITaskListShareRepository
{
    Task AddAsync(TaskListShare share, CancellationToken cancellationToken);

    /// <summary>
    /// Scoped to recipientUserId, the same way ITaskRepository.GetByIdAsync is scoped to an owner -
    /// returns null both when the share doesn't exist and when it exists but was offered to someone
    /// else, so a caller can't tell one case from the other by probing ids.
    /// </summary>
    Task<TaskListShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(TaskListShare share, CancellationToken cancellationToken);
}

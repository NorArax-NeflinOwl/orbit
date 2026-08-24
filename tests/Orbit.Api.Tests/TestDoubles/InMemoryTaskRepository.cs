using Orbit.Core.Tasks;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>
/// In-memory <see cref="ITaskRepository"/> stub for unit tests that need real add/lookup/update
/// behavior, including per-user ownership scoping, without spinning up SQLite.
/// </summary>
internal sealed class InMemoryTaskRepository : ITaskRepository
{
    private readonly List<TaskList> _taskLists = [];

    public Task<IReadOnlyList<TaskList>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<TaskList>>(_taskLists.Where(taskList => taskList.UserId == userId).ToList());

    public Task<TaskList?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
        => Task.FromResult(_taskLists.FirstOrDefault(taskList => taskList.Id == id && taskList.UserId == userId));

    public Task AddAsync(TaskList taskList, CancellationToken cancellationToken)
    {
        _taskLists.Add(taskList);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(TaskList taskList, CancellationToken cancellationToken)
    {
        // Handlers mutate the same TaskList instance this repository already holds a reference to, so
        // there is nothing to replace here - this mirrors how the EF Core repository just calls
        // SaveChangesAsync on an already-tracked entity.
        return Task.CompletedTask;
    }

    public Task UpdateManyAsync(IReadOnlyList<TaskList> taskLists, CancellationToken cancellationToken)
    {
        // Same reasoning as UpdateAsync - every list here is already the same tracked instance.
        return Task.CompletedTask;
    }

    public Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        _taskLists.RemoveAll(taskList => taskList.Id == id && taskList.UserId == userId);
        return Task.CompletedTask;
    }
}

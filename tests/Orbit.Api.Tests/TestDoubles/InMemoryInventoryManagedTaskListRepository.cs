using Orbit.Core.Inventory;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="IInventoryManagedTaskListRepository"/> stub for unit tests.</summary>
internal sealed class InMemoryInventoryManagedTaskListRepository : IInventoryManagedTaskListRepository
{
    private readonly Dictionary<Guid, Guid> _taskListIdByUserId = [];

    public Task<Guid?> GetTaskListIdAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult(_taskListIdByUserId.TryGetValue(userId, out var taskListId) ? taskListId : (Guid?)null);

    public Task SetTaskListIdAsync(Guid userId, Guid taskListId, CancellationToken cancellationToken)
    {
        _taskListIdByUserId[userId] = taskListId;
        return Task.CompletedTask;
    }
}

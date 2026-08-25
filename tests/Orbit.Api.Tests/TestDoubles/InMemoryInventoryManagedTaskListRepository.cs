using Orbit.Core.Inventory;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="IInventoryManagedTaskListRepository"/> stub for unit tests.</summary>
internal sealed class InMemoryInventoryManagedTaskListRepository : IInventoryManagedTaskListRepository
{
    private readonly Dictionary<Guid, Guid> _taskListIdByWarehouseId = [];

    public Task<Guid?> GetTaskListIdAsync(Guid warehouseId, CancellationToken cancellationToken)
        => Task.FromResult(_taskListIdByWarehouseId.TryGetValue(warehouseId, out var taskListId) ? taskListId : (Guid?)null);

    public Task SetTaskListIdAsync(Guid warehouseId, Guid taskListId, CancellationToken cancellationToken)
    {
        _taskListIdByWarehouseId[warehouseId] = taskListId;
        return Task.CompletedTask;
    }
}

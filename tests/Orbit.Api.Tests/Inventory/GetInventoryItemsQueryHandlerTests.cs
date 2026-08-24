using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.GetInventoryItems;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class GetInventoryItemsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_only_the_requesting_users_items()
    {
        var inventoryRepository = new InMemoryInventoryRepository();
        var taskRepository = new InMemoryTaskRepository();
        var handler = new GetInventoryItemsQueryHandler(inventoryRepository, new PendingRestockTaskResolver(taskRepository));
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        await inventoryRepository.AddAsync(InventoryItem.Create(userId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push), CancellationToken.None);
        await inventoryRepository.AddAsync(InventoryItem.Create(otherUserId, "Eggs", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push), CancellationToken.None);

        var results = await handler.HandleAsync(new GetInventoryItemsQuery(userId), CancellationToken.None);

        Assert.Equal("Milk", Assert.Single(results).Name);
    }

    [Fact]
    public async Task HandleAsync_clears_a_pending_restock_reference_once_the_linked_task_is_completed()
    {
        var inventoryRepository = new InMemoryInventoryRepository();
        var taskRepository = new InMemoryTaskRepository();
        var handler = new GetInventoryItemsQueryHandler(inventoryRepository, new PendingRestockTaskResolver(taskRepository));
        var userId = Guid.NewGuid();

        var restockItem = TaskItem.Create("Restock: Milk", dueDateUtc: null, isCompleted: true);
        var taskList = TaskList.Create(userId, "Restock supplies", [restockItem]);
        await taskRepository.AddAsync(taskList, CancellationToken.None);

        var item = InventoryItem.Create(userId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push);
        item.SetPendingRestockTask(taskList.Id, restockItem.Id);
        await inventoryRepository.AddAsync(item, CancellationToken.None);

        var results = await handler.HandleAsync(new GetInventoryItemsQuery(userId), CancellationToken.None);

        Assert.Null(Assert.Single(results).PendingRestockTaskItemId);
        // Persisted, not just returned - a later read should not need to re-resolve.
        var stored = await inventoryRepository.GetByIdAsync(userId, item.Id, CancellationToken.None);
        Assert.Null(stored!.PendingRestockTaskItemId);
    }
}

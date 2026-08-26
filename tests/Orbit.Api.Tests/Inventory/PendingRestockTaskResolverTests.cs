using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class PendingRestockTaskResolverTests
{
    [Fact]
    public async Task ResolveAsync_leaves_an_item_with_no_pending_task_untouched()
    {
        var context = new InventoryTestContext();
        var resolver = context.RestockTaskResolver;
        var item = InventoryItem.Create(context.AddWarehouse(Guid.NewGuid()), "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push);

        var result = await resolver.ResolveAsync(item, CancellationToken.None);

        Assert.Null(result.PendingRestockTaskListId);
    }

    [Fact]
    public async Task ResolveAsync_leaves_a_still_open_pending_task_untouched()
    {
        var context = new InventoryTestContext();
        var taskRepository = context.TaskRepository;
        var resolver = context.RestockTaskResolver;
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var restockItem = TaskItem.Create("Restock: Milk", dueDateUtc: null, isCompleted: false);
        var taskList = TaskList.Create(userId, "Restock supplies", [restockItem]);
        await taskRepository.AddAsync(taskList, CancellationToken.None);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push);
        item.SetPendingRestockTask(taskList.Id, restockItem.Id);

        var result = await resolver.ResolveAsync(item, CancellationToken.None);

        Assert.Equal(restockItem.Id, result.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task ResolveAsync_keeps_the_reference_when_the_linked_task_is_merely_completed()
    {
        var context = new InventoryTestContext();
        var taskRepository = context.TaskRepository;
        var resolver = context.RestockTaskResolver;
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var restockItem = TaskItem.Create("Restock: Milk", dueDateUtc: null, isCompleted: true);
        var taskList = TaskList.Create(userId, "Restock supplies", [restockItem]);
        await taskRepository.AddAsync(taskList, CancellationToken.None);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push);
        item.SetPendingRestockTask(taskList.Id, restockItem.Id);

        var result = await resolver.ResolveAsync(item, CancellationToken.None);

        // This used to clear the reference, which is what made the next save append a second
        // "Restock: Milk" beside the finished one - and a third the day after. A finished task is still
        // this item's task; it is reopened rather than duplicated.
        Assert.Equal(taskList.Id, result.PendingRestockTaskListId);
        Assert.Equal(restockItem.Id, result.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task ResolveAsync_clears_the_reference_when_the_linked_task_list_was_deleted()
    {
        var context = new InventoryTestContext();
        var taskRepository = context.TaskRepository;
        var resolver = context.RestockTaskResolver;
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var danglingTaskListId = Guid.NewGuid();
        var danglingTaskItemId = Guid.NewGuid();
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push);
        item.SetPendingRestockTask(danglingTaskListId, danglingTaskItemId);

        var result = await resolver.ResolveAsync(item, CancellationToken.None);

        Assert.Null(result.PendingRestockTaskListId);
        Assert.Null(result.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task ResolveAsync_clears_the_reference_when_the_linked_task_item_was_removed_from_its_list()
    {
        var context = new InventoryTestContext();
        var taskRepository = context.TaskRepository;
        var resolver = context.RestockTaskResolver;
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var taskList = TaskList.Create(userId, "Restock supplies", []);
        await taskRepository.AddAsync(taskList, CancellationToken.None);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push);
        item.SetPendingRestockTask(taskList.Id, Guid.NewGuid());

        var result = await resolver.ResolveAsync(item, CancellationToken.None);

        Assert.Null(result.PendingRestockTaskListId);
        Assert.Null(result.PendingRestockTaskItemId);
    }
}

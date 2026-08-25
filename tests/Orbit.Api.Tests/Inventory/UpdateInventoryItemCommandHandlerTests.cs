using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.UpdateInventoryItem;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class UpdateInventoryItemCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_updates_an_item_owned_by_the_requesting_user()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var inventoryRepository = context.InventoryRepository;
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push);
        await inventoryRepository.AddAsync(item, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new UpdateInventoryItemCommand(userId, warehouseId, item.Id, "Whole milk", "Dairy", "Fridge", 3m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = await inventoryRepository.GetByIdAsync(warehouseId, item.Id, CancellationToken.None);
        Assert.Equal("Whole milk", stored!.Name);
        Assert.Equal(3m, stored.Quantity);
    }

    [Fact]
    public async Task HandleAsync_returns_not_found_for_a_warehouse_the_caller_cannot_reach()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var inventoryRepository = context.InventoryRepository;
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerId);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push);
        await inventoryRepository.AddAsync(item, CancellationToken.None);

        var outcome = await handler.HandleAsync(
            new UpdateInventoryItemCommand(otherUserId, warehouseId, item.Id, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_creates_a_restock_task_when_quantity_drops_to_the_minimum()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var inventoryRepository = context.InventoryRepository;
        var taskRepository = context.TaskRepository;
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 5m, 1m, null, NotificationChannel.Push);
        await inventoryRepository.AddAsync(item, CancellationToken.None);

        await handler.HandleAsync(
            new UpdateInventoryItemCommand(userId, warehouseId, item.Id, "Milk", "Dairy", "Fridge", 1m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        var stored = await inventoryRepository.GetByIdAsync(warehouseId, item.Id, CancellationToken.None);
        Assert.NotNull(stored!.PendingRestockTaskItemId);
        var taskList = await taskRepository.GetByIdAsync(userId, stored.PendingRestockTaskListId!.Value, CancellationToken.None);
        Assert.Contains(taskList!.Items, taskItem => taskItem.Id == stored.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task HandleAsync_does_not_create_a_second_restock_task_while_one_is_already_open()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var inventoryRepository = context.InventoryRepository;
        var taskRepository = context.TaskRepository;
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push);
        await inventoryRepository.AddAsync(item, CancellationToken.None);
        await handler.HandleAsync(
            new UpdateInventoryItemCommand(userId, warehouseId, item.Id, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);
        var firstPendingTaskItemId = (await inventoryRepository.GetByIdAsync(warehouseId, item.Id, CancellationToken.None))!.PendingRestockTaskItemId;

        // Still below minimum on a second, unrelated edit - should not spawn a duplicate restock task.
        await handler.HandleAsync(
            new UpdateInventoryItemCommand(userId, warehouseId, item.Id, "Whole milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        var stored = await inventoryRepository.GetByIdAsync(warehouseId, item.Id, CancellationToken.None);
        Assert.Equal(firstPendingTaskItemId, stored!.PendingRestockTaskItemId);
        var taskList = await taskRepository.GetByIdAsync(userId, stored.PendingRestockTaskListId!.Value, CancellationToken.None);
        Assert.Single(taskList!.Items, taskItem => taskItem.Description == "Restock: Milk" || taskItem.Description == "Restock: Whole milk");
    }

    [Fact]
    public async Task HandleAsync_clears_the_pending_restock_task_reference_once_quantity_rises_above_minimum()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var inventoryRepository = context.InventoryRepository;
        var taskRepository = context.TaskRepository;
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push);
        await inventoryRepository.AddAsync(item, CancellationToken.None);
        await handler.HandleAsync(
            new UpdateInventoryItemCommand(userId, warehouseId, item.Id, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);
        var pending = await inventoryRepository.GetByIdAsync(warehouseId, item.Id, CancellationToken.None);
        var taskListId = pending!.PendingRestockTaskListId!.Value;
        var taskItemId = pending.PendingRestockTaskItemId!.Value;

        await handler.HandleAsync(
            new UpdateInventoryItemCommand(userId, warehouseId, item.Id, "Milk", "Dairy", "Fridge", 5m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        var stored = await inventoryRepository.GetByIdAsync(warehouseId, item.Id, CancellationToken.None);
        Assert.Null(stored!.PendingRestockTaskListId);
        Assert.Null(stored.PendingRestockTaskItemId);

        // The already-created restock task itself is left alone, not deleted.
        var taskList = await taskRepository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        Assert.Contains(taskList!.Items, taskItem => taskItem.Id == taskItemId);
    }

    private static UpdateInventoryItemCommandHandler CreateHandler(InventoryTestContext context)
        => new(context.InventoryRepository, context.AccessResolver, context.TaskListCoordinator);
}

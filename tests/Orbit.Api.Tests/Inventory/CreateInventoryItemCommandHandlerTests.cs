using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.CreateInventoryItem;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class CreateInventoryItemCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_an_item_in_the_requested_warehouse()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        var itemId = await handler.HandleAsync(
            new CreateInventoryItemCommand(userId, warehouseId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        var stored = await context.InventoryRepository.GetByIdAsync(warehouseId, itemId!.Value, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Milk", stored!.Name);
        Assert.Equal(warehouseId, stored.WarehouseId);
        Assert.False(stored.IsBelowMinimum);
        Assert.Null(stored.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task HandleAsync_creates_a_restock_task_when_the_new_item_starts_below_minimum()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        var itemId = await handler.HandleAsync(
            new CreateInventoryItemCommand(userId, warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        var stored = await context.InventoryRepository.GetByIdAsync(warehouseId, itemId!.Value, CancellationToken.None);
        Assert.NotNull(stored!.PendingRestockTaskListId);
        Assert.NotNull(stored.PendingRestockTaskItemId);

        var taskList = await context.TaskRepository.GetByIdAsync(userId, stored.PendingRestockTaskListId!.Value, CancellationToken.None);
        Assert.NotNull(taskList);
        Assert.Contains(taskList!.Items, item => item.Id == stored.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task HandleAsync_creates_the_standing_reminder_task_the_first_time_any_item_lands_in_a_warehouse()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        // Above minimum - no restock task expected, but the standing reminder should still be created.
        await handler.HandleAsync(
            new CreateInventoryItemCommand(userId, warehouseId, "Milk", "Dairy", "Fridge", 5m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        var taskListId = await context.ManagedTaskListRepository.GetTaskListIdAsync(warehouseId, CancellationToken.None);
        Assert.NotNull(taskListId);
        var taskList = await context.TaskRepository.GetByIdAsync(userId, taskListId!.Value, CancellationToken.None);
        Assert.NotNull(taskList);
        Assert.Contains(taskList!.Items, item => item.Description == InventoryTaskListCoordinator.UpdateStockReminderDescription && item.RemindDaily);
    }

    [Fact]
    public async Task HandleAsync_gives_each_warehouse_its_own_managed_task_list()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var kitchenId = context.AddWarehouse(userId, "Kitchen");
        var garageId = context.AddWarehouse(userId, "Garage");

        await handler.HandleAsync(
            new CreateInventoryItemCommand(userId, kitchenId, "Milk", "Dairy", "Fridge", 5m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);
        await handler.HandleAsync(
            new CreateInventoryItemCommand(userId, garageId, "Screws", "Hardware", "Shelf", 5m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        var kitchenTaskListId = await context.ManagedTaskListRepository.GetTaskListIdAsync(kitchenId, CancellationToken.None);
        var garageTaskListId = await context.ManagedTaskListRepository.GetTaskListIdAsync(garageId, CancellationToken.None);
        Assert.NotNull(kitchenTaskListId);
        Assert.NotNull(garageTaskListId);
        Assert.NotEqual(kitchenTaskListId, garageTaskListId);
    }

    [Fact]
    public async Task HandleAsync_refuses_to_add_an_item_to_a_warehouse_the_caller_cannot_reach()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var warehouseId = context.AddWarehouse(Guid.NewGuid());

        var itemId = await handler.HandleAsync(
            new CreateInventoryItemCommand(Guid.NewGuid(), warehouseId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        Assert.Null(itemId);
    }

    [Fact]
    public async Task HandleAsync_refuses_to_add_an_item_when_the_share_is_read_only()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        context.AddAcceptedShare(warehouseId, ownerUserId, recipientUserId, ShareAccessLevel.ReadOnly);

        var itemId = await handler.HandleAsync(
            new CreateInventoryItemCommand(recipientUserId, warehouseId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        Assert.Null(itemId);
    }

    [Fact]
    public async Task HandleAsync_lets_a_can_edit_recipient_add_an_item()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        context.AddAcceptedShare(warehouseId, ownerUserId, recipientUserId, ShareAccessLevel.CanEdit);

        var itemId = await handler.HandleAsync(
            new CreateInventoryItemCommand(recipientUserId, warehouseId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        Assert.NotNull(itemId);
        var stored = await context.InventoryRepository.GetByIdAsync(warehouseId, itemId!.Value, CancellationToken.None);
        Assert.Equal(warehouseId, stored!.WarehouseId);
    }

    private static CreateInventoryItemCommandHandler CreateHandler(InventoryTestContext context)
        => new(context.InventoryRepository, context.AccessResolver, context.TaskListCoordinator);
}

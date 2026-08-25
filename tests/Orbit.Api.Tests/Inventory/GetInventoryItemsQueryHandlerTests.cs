using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.GetInventoryItems;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class GetInventoryItemsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_only_the_requested_warehouses_items()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var kitchenId = context.AddWarehouse(userId, "Kitchen");
        var garageId = context.AddWarehouse(userId, "Garage");
        await context.InventoryRepository.AddAsync(
            InventoryItem.Create(kitchenId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push), CancellationToken.None);
        await context.InventoryRepository.AddAsync(
            InventoryItem.Create(garageId, "Screws", "Hardware", "Shelf", 2m, 1m, null, NotificationChannel.Push), CancellationToken.None);

        var results = await handler.HandleAsync(new GetInventoryItemsQuery(userId, kitchenId), CancellationToken.None);

        Assert.Equal("Milk", Assert.Single(results!).Name);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_warehouse_the_caller_cannot_reach()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var warehouseId = context.AddWarehouse(Guid.NewGuid());

        var results = await handler.HandleAsync(new GetInventoryItemsQuery(Guid.NewGuid(), warehouseId), CancellationToken.None);

        Assert.Null(results);
    }

    [Fact]
    public async Task HandleAsync_returns_items_to_a_read_only_share_recipient()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        context.AddAcceptedShare(warehouseId, ownerUserId, recipientUserId, ShareAccessLevel.ReadOnly);
        await context.InventoryRepository.AddAsync(
            InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push), CancellationToken.None);

        var results = await handler.HandleAsync(new GetInventoryItemsQuery(recipientUserId, warehouseId), CancellationToken.None);

        Assert.Equal("Milk", Assert.Single(results!).Name);
    }

    [Fact]
    public async Task HandleAsync_clears_a_pending_restock_reference_once_the_linked_task_is_completed()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        var restockItem = TaskItem.Create("Restock: Milk", dueDateUtc: null, isCompleted: true);
        var taskList = TaskList.Create(userId, "Restock supplies", [restockItem]);
        await context.TaskRepository.AddAsync(taskList, CancellationToken.None);

        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push);
        item.SetPendingRestockTask(taskList.Id, restockItem.Id);
        await context.InventoryRepository.AddAsync(item, CancellationToken.None);

        var results = await handler.HandleAsync(new GetInventoryItemsQuery(userId, warehouseId), CancellationToken.None);

        Assert.Null(Assert.Single(results!).PendingRestockTaskItemId);
        // Persisted, not just returned - a later read should not need to re-resolve.
        var stored = await context.InventoryRepository.GetByIdAsync(warehouseId, item.Id, CancellationToken.None);
        Assert.Null(stored!.PendingRestockTaskItemId);
    }

    private static GetInventoryItemsQueryHandler CreateHandler(InventoryTestContext context)
        => new(context.InventoryRepository, context.AccessResolver, context.RestockTaskResolver);
}

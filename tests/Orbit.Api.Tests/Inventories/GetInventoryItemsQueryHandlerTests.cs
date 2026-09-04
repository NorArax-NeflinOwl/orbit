using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.GetInventoryItems;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

public sealed class GetInventoryItemsQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_only_the_requested_inventories_items()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var kitchenId = context.AddInventory(userId, "Kitchen");
        var garageId = context.AddInventory(userId, "Garage");
        await context.InventoryItemRepository.AddAsync(
            InventoryItem.Create(kitchenId, "Milk", "Dairy", ["Fridge"], 2m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push), CancellationToken.None);
        await context.InventoryItemRepository.AddAsync(
            InventoryItem.Create(garageId, "Screws", "Hardware", ["Shelf"], 2m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push), CancellationToken.None);

        var results = await handler.HandleAsync(new GetInventoryItemsQuery(userId, kitchenId), CancellationToken.None);

        Assert.Equal("Milk", Assert.Single(results!).Name);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_inventory_the_caller_cannot_reach()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var inventoryId = context.AddInventory(Guid.NewGuid());

        var results = await handler.HandleAsync(new GetInventoryItemsQuery(Guid.NewGuid(), inventoryId), CancellationToken.None);

        Assert.Null(results);
    }

    [Fact]
    public async Task HandleAsync_returns_items_to_a_read_only_share_recipient()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId);
        context.AddAcceptedShare(inventoryId, ownerUserId, recipientUserId, ShareAccessLevel.ReadOnly);
        await context.InventoryItemRepository.AddAsync(
            InventoryItem.Create(inventoryId, "Milk", "Dairy", ["Fridge"], 2m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push), CancellationToken.None);

        var results = await handler.HandleAsync(new GetInventoryItemsQuery(recipientUserId, inventoryId), CancellationToken.None);

        Assert.Equal("Milk", Assert.Single(results!).Name);
    }

    [Fact]
    public async Task HandleAsync_clears_a_pending_restock_reference_once_the_linked_task_is_gone()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);

        // The list the reference points at was deleted; completing the task no longer counts as losing
        // it, since that is what let a second "Restock: Milk" appear beside the finished one.
        var item = InventoryItem.Create(inventoryId, "Milk", "Dairy", ["Fridge"], 0m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);
        item.SetPendingRestockTask(Guid.NewGuid(), Guid.NewGuid());
        await context.InventoryItemRepository.AddAsync(item, CancellationToken.None);

        var results = await handler.HandleAsync(new GetInventoryItemsQuery(userId, inventoryId), CancellationToken.None);

        Assert.Null(Assert.Single(results!).PendingRestockTaskItemId);
        // Persisted, not just returned - a later read should not need to re-resolve.
        var stored = await context.InventoryItemRepository.GetByIdAsync(inventoryId, item.Id, CancellationToken.None);
        Assert.Null(stored!.PendingRestockTaskItemId);
    }

    private static GetInventoryItemsQueryHandler CreateHandler(InventoryTestContext context)
        => new(context.InventoryItemRepository, context.AccessResolver, context.RestockTaskResolver);
}

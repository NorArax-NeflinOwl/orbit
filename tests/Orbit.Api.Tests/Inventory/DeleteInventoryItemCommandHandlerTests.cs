using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.DeleteInventoryItem;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class DeleteInventoryItemCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_deletes_an_item_from_a_warehouse_the_caller_owns()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var item = await AddItemAsync(context, warehouseId);

        var wasDeleted = await handler.HandleAsync(new DeleteInventoryItemCommand(userId, warehouseId, item.Id), CancellationToken.None);

        Assert.True(wasDeleted);
        Assert.Null(await context.InventoryRepository.GetByIdAsync(warehouseId, item.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_a_warehouse_the_caller_cannot_reach()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var warehouseId = context.AddWarehouse(Guid.NewGuid());
        var item = await AddItemAsync(context, warehouseId);

        var wasDeleted = await handler.HandleAsync(
            new DeleteInventoryItemCommand(Guid.NewGuid(), warehouseId, item.Id), CancellationToken.None);

        Assert.False(wasDeleted);
        Assert.NotNull(await context.InventoryRepository.GetByIdAsync(warehouseId, item.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_when_the_callers_share_is_read_only()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        context.AddAcceptedShare(warehouseId, ownerUserId, recipientUserId, ShareAccessLevel.ReadOnly);
        var item = await AddItemAsync(context, warehouseId);

        var wasDeleted = await handler.HandleAsync(
            new DeleteInventoryItemCommand(recipientUserId, warehouseId, item.Id), CancellationToken.None);

        Assert.False(wasDeleted);
        Assert.NotNull(await context.InventoryRepository.GetByIdAsync(warehouseId, item.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_item_id()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        var wasDeleted = await handler.HandleAsync(
            new DeleteInventoryItemCommand(userId, warehouseId, Guid.NewGuid()), CancellationToken.None);

        Assert.False(wasDeleted);
    }

    private static DeleteInventoryItemCommandHandler CreateHandler(InventoryTestContext context)
        => new(context.InventoryRepository, context.AccessResolver);

    private static async Task<InventoryItem> AddItemAsync(InventoryTestContext context, Guid warehouseId)
    {
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push);
        await context.InventoryRepository.AddAsync(item, CancellationToken.None);
        return item;
    }
}

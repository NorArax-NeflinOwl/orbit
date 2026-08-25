using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.GetInventoryItemById;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class GetInventoryItemByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_an_item_from_a_warehouse_the_caller_owns()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var item = await AddItemAsync(context, warehouseId);

        var result = await handler.HandleAsync(new GetInventoryItemByIdQuery(userId, warehouseId, item.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Milk", result!.Name);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_a_warehouse_the_caller_cannot_reach()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var warehouseId = context.AddWarehouse(Guid.NewGuid());
        var item = await AddItemAsync(context, warehouseId);

        var result = await handler.HandleAsync(
            new GetInventoryItemByIdQuery(Guid.NewGuid(), warehouseId, item.Id), CancellationToken.None);

        Assert.Null(result);
    }

    [Fact]
    public async Task HandleAsync_returns_an_item_to_a_read_only_share_recipient()
    {
        var context = new InventoryTestContext();
        var handler = CreateHandler(context);
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        context.AddAcceptedShare(warehouseId, ownerUserId, recipientUserId, ShareAccessLevel.ReadOnly);
        var item = await AddItemAsync(context, warehouseId);

        var result = await handler.HandleAsync(
            new GetInventoryItemByIdQuery(recipientUserId, warehouseId, item.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Milk", result!.Name);
    }

    private static GetInventoryItemByIdQueryHandler CreateHandler(InventoryTestContext context)
        => new(context.InventoryRepository, context.AccessResolver, context.RestockTaskResolver);

    private static async Task<InventoryItem> AddItemAsync(InventoryTestContext context, Guid warehouseId)
    {
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push);
        await context.InventoryRepository.AddAsync(item, CancellationToken.None);
        return item;
    }
}

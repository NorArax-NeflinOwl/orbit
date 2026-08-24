using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.GetInventoryItemById;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class GetInventoryItemByIdQueryHandlerTests
{
    [Fact]
    public async Task HandleAsync_returns_an_item_owned_by_the_requesting_user()
    {
        var inventoryRepository = new InMemoryInventoryRepository();
        var handler = new GetInventoryItemByIdQueryHandler(inventoryRepository, new PendingRestockTaskResolver(new InMemoryTaskRepository()));
        var userId = Guid.NewGuid();
        var item = InventoryItem.Create(userId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push);
        await inventoryRepository.AddAsync(item, CancellationToken.None);

        var result = await handler.HandleAsync(new GetInventoryItemByIdQuery(userId, item.Id), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("Milk", result!.Name);
    }

    [Fact]
    public async Task HandleAsync_returns_null_for_an_item_owned_by_a_different_user()
    {
        var inventoryRepository = new InMemoryInventoryRepository();
        var handler = new GetInventoryItemByIdQueryHandler(inventoryRepository, new PendingRestockTaskResolver(new InMemoryTaskRepository()));
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var item = InventoryItem.Create(ownerId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push);
        await inventoryRepository.AddAsync(item, CancellationToken.None);

        var result = await handler.HandleAsync(new GetInventoryItemByIdQuery(otherUserId, item.Id), CancellationToken.None);

        Assert.Null(result);
    }
}

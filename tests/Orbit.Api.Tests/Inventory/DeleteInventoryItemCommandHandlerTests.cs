using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.DeleteInventoryItem;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class DeleteInventoryItemCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_deletes_an_item_owned_by_the_requesting_user()
    {
        var repository = new InMemoryInventoryRepository();
        var handler = new DeleteInventoryItemCommandHandler(repository);
        var userId = Guid.NewGuid();
        var item = InventoryItem.Create(userId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push);
        await repository.AddAsync(item, CancellationToken.None);

        var wasDeleted = await handler.HandleAsync(new DeleteInventoryItemCommand(userId, item.Id), CancellationToken.None);

        Assert.True(wasDeleted);
        Assert.Null(await repository.GetByIdAsync(userId, item.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_and_does_not_delete_an_item_owned_by_a_different_user()
    {
        var repository = new InMemoryInventoryRepository();
        var handler = new DeleteInventoryItemCommandHandler(repository);
        var ownerId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var item = InventoryItem.Create(ownerId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push);
        await repository.AddAsync(item, CancellationToken.None);

        var wasDeleted = await handler.HandleAsync(new DeleteInventoryItemCommand(otherUserId, item.Id), CancellationToken.None);

        Assert.False(wasDeleted);
        Assert.NotNull(await repository.GetByIdAsync(ownerId, item.Id, CancellationToken.None));
    }

    [Fact]
    public async Task HandleAsync_returns_false_for_an_unknown_item_id()
    {
        var handler = new DeleteInventoryItemCommandHandler(new InMemoryInventoryRepository());

        var wasDeleted = await handler.HandleAsync(new DeleteInventoryItemCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        Assert.False(wasDeleted);
    }
}

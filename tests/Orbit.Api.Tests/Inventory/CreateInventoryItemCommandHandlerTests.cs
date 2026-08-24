using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.CreateInventoryItem;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class CreateInventoryItemCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_creates_an_item_owned_by_the_requesting_user()
    {
        var (handler, inventoryRepository, _, _) = CreateHandler();
        var userId = Guid.NewGuid();

        var itemId = await handler.HandleAsync(
            new CreateInventoryItemCommand(userId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        var stored = await inventoryRepository.GetByIdAsync(userId, itemId, CancellationToken.None);
        Assert.NotNull(stored);
        Assert.Equal("Milk", stored!.Name);
        Assert.False(stored.IsBelowMinimum);
        Assert.Null(stored.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task HandleAsync_creates_a_restock_task_when_the_new_item_starts_below_minimum()
    {
        var (handler, inventoryRepository, taskRepository, _) = CreateHandler();
        var userId = Guid.NewGuid();

        var itemId = await handler.HandleAsync(
            new CreateInventoryItemCommand(userId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        var stored = await inventoryRepository.GetByIdAsync(userId, itemId, CancellationToken.None);
        Assert.NotNull(stored!.PendingRestockTaskListId);
        Assert.NotNull(stored.PendingRestockTaskItemId);

        var taskList = await taskRepository.GetByIdAsync(userId, stored.PendingRestockTaskListId!.Value, CancellationToken.None);
        Assert.NotNull(taskList);
        Assert.Contains(taskList!.Items, item => item.Id == stored.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task HandleAsync_creates_the_standing_reminder_task_the_first_time_a_user_adds_any_item()
    {
        var (handler, _, taskRepository, managedTaskListRepository) = CreateHandler();
        var userId = Guid.NewGuid();

        // Above minimum - no restock task expected, but the standing reminder should still be created.
        await handler.HandleAsync(
            new CreateInventoryItemCommand(userId, "Milk", "Dairy", "Fridge", 5m, 1m, null, NotificationChannel.Push),
            CancellationToken.None);

        var taskListId = await managedTaskListRepository.GetTaskListIdAsync(userId, CancellationToken.None);
        Assert.NotNull(taskListId);
        var taskList = await taskRepository.GetByIdAsync(userId, taskListId!.Value, CancellationToken.None);
        Assert.NotNull(taskList);
        Assert.Contains(taskList!.Items, item => item.Description == InventoryTaskListCoordinator.UpdateStockReminderDescription && item.RemindDaily);
    }

    private static (
        CreateInventoryItemCommandHandler Handler, InMemoryInventoryRepository InventoryRepository,
        InMemoryTaskRepository TaskRepository, InMemoryInventoryManagedTaskListRepository ManagedTaskListRepository) CreateHandler()
    {
        var inventoryRepository = new InMemoryInventoryRepository();
        var taskRepository = new InMemoryTaskRepository();
        var managedTaskListRepository = new InMemoryInventoryManagedTaskListRepository();
        var resolver = new PendingRestockTaskResolver(taskRepository);
        var coordinator = new InventoryTaskListCoordinator(taskRepository, managedTaskListRepository, resolver);
        var handler = new CreateInventoryItemCommandHandler(inventoryRepository, coordinator);
        return (handler, inventoryRepository, taskRepository, managedTaskListRepository);
    }
}

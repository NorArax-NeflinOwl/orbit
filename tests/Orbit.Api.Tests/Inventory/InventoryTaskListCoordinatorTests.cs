using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

public sealed class InventoryTaskListCoordinatorTests
{
    [Fact]
    public async Task EnsureManagedTaskListAsync_creates_the_list_with_the_standing_reminder_item_the_first_time()
    {
        var (coordinator, taskRepository, managedTaskListRepository) = CreateCoordinator();
        var userId = Guid.NewGuid();

        var taskListId = await coordinator.EnsureManagedTaskListAsync(userId, CancellationToken.None);

        var taskList = await taskRepository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        Assert.NotNull(taskList);
        Assert.Equal(InventoryTaskListCoordinator.ManagedTaskListTitle, taskList!.Title);
        var reminder = Assert.Single(taskList.Items);
        Assert.Equal(InventoryTaskListCoordinator.UpdateStockReminderDescription, reminder.Description);
        Assert.True(reminder.RemindDaily);
        Assert.Equal(taskListId, await managedTaskListRepository.GetTaskListIdAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureManagedTaskListAsync_reuses_the_existing_list_on_a_second_call()
    {
        var (coordinator, _, _) = CreateCoordinator();
        var userId = Guid.NewGuid();

        var firstId = await coordinator.EnsureManagedTaskListAsync(userId, CancellationToken.None);
        var secondId = await coordinator.EnsureManagedTaskListAsync(userId, CancellationToken.None);

        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task EnsureManagedTaskListAsync_creates_a_fresh_list_if_the_tracked_one_was_deleted()
    {
        var (coordinator, taskRepository, managedTaskListRepository) = CreateCoordinator();
        var userId = Guid.NewGuid();
        var firstId = await coordinator.EnsureManagedTaskListAsync(userId, CancellationToken.None);
        await taskRepository.DeleteAsync(userId, firstId, CancellationToken.None);

        var secondId = await coordinator.EnsureManagedTaskListAsync(userId, CancellationToken.None);

        Assert.NotEqual(firstId, secondId);
        Assert.NotNull(await taskRepository.GetByIdAsync(userId, secondId, CancellationToken.None));
        Assert.Equal(secondId, await managedTaskListRepository.GetTaskListIdAsync(userId, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureRestockTaskAsync_is_a_no_op_when_the_item_is_not_below_minimum()
    {
        var (coordinator, _, _) = CreateCoordinator();
        var item = InventoryItem.Create(Guid.NewGuid(), "Milk", "Dairy", "Fridge", 5m, 1m, null, NotificationChannel.Push);

        var result = await coordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        Assert.Null(result.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task EnsureRestockTaskAsync_appends_a_restock_item_when_below_minimum()
    {
        var (coordinator, taskRepository, _) = CreateCoordinator();
        var item = InventoryItem.Create(Guid.NewGuid(), "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push);

        var result = await coordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        Assert.NotNull(result.PendingRestockTaskListId);
        Assert.NotNull(result.PendingRestockTaskItemId);
        var taskList = await taskRepository.GetByIdAsync(item.UserId, result.PendingRestockTaskListId!.Value, CancellationToken.None);
        Assert.Contains(taskList!.Items, taskItem => taskItem.Id == result.PendingRestockTaskItemId && taskItem.Description == "Uzupełnij: Milk");
    }

    private static (
        InventoryTaskListCoordinator Coordinator, InMemoryTaskRepository TaskRepository,
        InMemoryInventoryManagedTaskListRepository ManagedTaskListRepository) CreateCoordinator()
    {
        var taskRepository = new InMemoryTaskRepository();
        var managedTaskListRepository = new InMemoryInventoryManagedTaskListRepository();
        var resolver = new PendingRestockTaskResolver(taskRepository);
        var coordinator = new InventoryTaskListCoordinator(taskRepository, managedTaskListRepository, resolver);
        return (coordinator, taskRepository, managedTaskListRepository);
    }
}

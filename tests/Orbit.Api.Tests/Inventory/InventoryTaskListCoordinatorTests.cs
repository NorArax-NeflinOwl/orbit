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
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);

        var taskList = await context.TaskRepository.GetByIdAsync(userId, taskListId!.Value, CancellationToken.None);
        Assert.NotNull(taskList);
        Assert.Equal(InventoryTaskListCoordinator.ManagedTaskListTitle, taskList!.Title);
        var reminder = Assert.Single(taskList.Items);
        Assert.Equal(InventoryTaskListCoordinator.UpdateStockReminderDescription, reminder.Description);
        Assert.True(reminder.RemindDaily);
        Assert.Equal(taskListId, await context.ManagedTaskListRepository.GetTaskListIdAsync(warehouseId, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureManagedTaskListAsync_creates_the_list_under_the_warehouse_owner_not_the_caller()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);

        // Scoped to the owner: a share recipient's own task lists must not gain the owner's restock list.
        Assert.NotNull(await context.TaskRepository.GetByIdAsync(ownerUserId, taskListId!.Value, CancellationToken.None));
        Assert.Null(await context.TaskRepository.GetByIdAsync(Guid.NewGuid(), taskListId.Value, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureManagedTaskListAsync_reuses_the_existing_list_on_a_second_call()
    {
        var context = new InventoryTestContext();
        var warehouseId = context.AddWarehouse(Guid.NewGuid());

        var firstId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);
        var secondId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);

        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task EnsureManagedTaskListAsync_creates_a_fresh_list_if_the_tracked_one_was_deleted()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var firstId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);
        await context.TaskRepository.DeleteAsync(userId, firstId!.Value, CancellationToken.None);

        var secondId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);

        Assert.NotEqual(firstId, secondId);
        Assert.NotNull(await context.TaskRepository.GetByIdAsync(userId, secondId!.Value, CancellationToken.None));
        Assert.Equal(secondId, await context.ManagedTaskListRepository.GetTaskListIdAsync(warehouseId, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureManagedTaskListAsync_returns_null_for_a_warehouse_that_no_longer_exists()
    {
        var context = new InventoryTestContext();

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(taskListId);
    }

    [Fact]
    public async Task EnsureRestockTaskAsync_is_a_no_op_when_the_item_is_not_below_minimum()
    {
        var context = new InventoryTestContext();
        var warehouseId = context.AddWarehouse(Guid.NewGuid());
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 5m, 1m, null, NotificationChannel.Push);

        var result = await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        Assert.Null(result.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task EnsureRestockTaskAsync_appends_a_restock_item_when_below_minimum()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, null, NotificationChannel.Push);

        var result = await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        Assert.NotNull(result.PendingRestockTaskListId);
        Assert.NotNull(result.PendingRestockTaskItemId);
        var taskList = await context.TaskRepository.GetByIdAsync(userId, result.PendingRestockTaskListId!.Value, CancellationToken.None);
        Assert.Contains(taskList!.Items, taskItem => taskItem.Id == result.PendingRestockTaskItemId && taskItem.Description == "Restock: Milk");
    }
}

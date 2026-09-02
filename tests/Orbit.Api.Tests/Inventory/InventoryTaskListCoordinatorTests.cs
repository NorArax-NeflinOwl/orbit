using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Tasks;
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
        Assert.Equal(RestockTaskNaming.TitleFor("Kitchen"), taskList!.Title);
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
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 5m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);

        var result = await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        Assert.Null(result.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task EnsureRestockTaskAsync_appends_a_restock_item_when_below_minimum()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);

        var result = await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        Assert.NotNull(result.PendingRestockTaskListId);
        Assert.NotNull(result.PendingRestockTaskItemId);
        var taskList = await context.TaskRepository.GetByIdAsync(userId, result.PendingRestockTaskListId!.Value, CancellationToken.None);
        var raised = Assert.Single(taskList!.Items, taskItem => taskItem.Id == result.PendingRestockTaskItemId);
        // The number is the minimum the shelf is meant to hold, so the errand can be read on its own.
        Assert.Equal("Restock: Milk (1)", raised.Description);
    }
    [Fact]
    public async Task A_product_that_is_still_low_reopens_its_task_instead_of_growing_a_second_one()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);
        item = await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        // The reader restocks, ticks the task off - and the product is still under its minimum.
        var taskListId = item.PendingRestockTaskListId!.Value;
        var taskList = await context.TaskRepository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        var completed = taskList!.Items.Select(existing => existing.Id == item.PendingRestockTaskItemId
            ? TaskItem.FromPersistence(existing.Id, existing.Description, existing.DueDateUtc, isCompleted: true,
                existing.LinkedTaskListIds, existing.Reminders)
            : existing).ToList();
        taskList.Update(
            taskList.Title, completed, taskList.IsGroup, taskList.IsPrivate, taskList.EncryptedContent, taskList.Priority);
        await context.TaskRepository.UpdateAsync(taskList, CancellationToken.None);

        var result = await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        // One entry, brought back - not a fresh one beside the finished one, which is how a product that
        // stayed low grew a new "Restock: Milk" on every save.
        var afterwards = await context.TaskRepository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        var restockEntries = afterwards!.Items
            .Where(taskItem => RestockTaskNaming.ProductIn(taskItem.Description) == "Milk")
            .ToList();
        Assert.Single(restockEntries);
        Assert.False(restockEntries[0].IsCompleted);
        Assert.Equal(item.PendingRestockTaskItemId, result.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task An_open_restock_task_is_left_exactly_as_it_is()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);
        item = await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        var result = await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        var taskList = await context.TaskRepository.GetByIdAsync(userId, result.PendingRestockTaskListId!.Value, CancellationToken.None);
        Assert.Single(taskList!.Items, taskItem => RestockTaskNaming.ProductIn(taskItem.Description) == "Milk");
    }

    /// <summary>
    /// A restock list is an ordinary task list once it exists: its owner can mark it High, pin it, and
    /// otherwise treat it as theirs. Everything this coordinator does to one is an append or a rename,
    /// so nothing here may quietly reset the rest of it - which is exactly what happened while
    /// TaskList.Update took the priority as an optional parameter and these three call sites left it
    /// out. Marking the list High and letting the warehouse touch it dropped it back to Normal.
    /// </summary>
    private static async Task<TaskList> MarkHighPriorityAsync(InventoryTestContext context, Guid userId, Guid taskListId)
    {
        var taskList = await context.TaskRepository.GetByIdAsync(userId, taskListId, CancellationToken.None);
        taskList!.Update(
            taskList.Title, taskList.Items, taskList.IsGroup, taskList.IsPrivate, taskList.EncryptedContent,
            ItemPriority.High);
        await context.TaskRepository.UpdateAsync(taskList, CancellationToken.None);
        return taskList;
    }

    [Fact]
    public async Task Raising_a_restock_errand_leaves_the_list_as_important_as_it_was()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);
        await MarkHighPriorityAsync(context, userId, taskListId!.Value);

        var item = InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 0m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);
        await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        var afterwards = await context.TaskRepository.GetByIdAsync(userId, taskListId.Value, CancellationToken.None);
        Assert.Equal(ItemPriority.High, afterwards!.Priority);
    }

    [Fact]
    public async Task Raising_a_shortfall_leaves_the_list_as_important_as_it_was()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);
        await MarkHighPriorityAsync(context, userId, taskListId!.Value);

        await context.TaskListCoordinator.EnsureShortfallTasksAsync(
            warehouseId, [new RestockNeed("Flour", 5m)], CancellationToken.None);

        var afterwards = await context.TaskRepository.GetByIdAsync(userId, taskListId.Value, CancellationToken.None);
        Assert.Equal(ItemPriority.High, afterwards!.Priority);
    }

    [Fact]
    public async Task Renaming_the_warehouse_leaves_its_list_as_important_as_it_was()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);
        await MarkHighPriorityAsync(context, userId, taskListId!.Value);

        var warehouse = await context.WarehouseRepository.GetByIdAsync(userId, warehouseId, CancellationToken.None);
        warehouse!.Update("Pantry", isPrivate: false, encryptedContent: null);
        await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);

        var afterwards = await context.TaskRepository.GetByIdAsync(userId, taskListId.Value, CancellationToken.None);
        Assert.Equal(RestockTaskNaming.TitleFor("Pantry"), afterwards!.Title);
        Assert.Equal(ItemPriority.High, afterwards.Priority);
    }

    [Fact]
    public async Task The_standing_reminder_comes_back_at_a_waking_hour()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(warehouseId, CancellationToken.None);

        var taskList = await context.TaskRepository.GetByIdAsync(userId, taskListId!.Value, CancellationToken.None);
        var reminder = Assert.Single(taskList!.Items);
        // A bare TimeOnly defaults to midnight, which is a reminder nobody is awake to act on.
        Assert.True(reminder.RemindDaily);
        Assert.Equal(new TimeOnly(9, 0), reminder.DailyReminderTimeOfDay);
    }
}
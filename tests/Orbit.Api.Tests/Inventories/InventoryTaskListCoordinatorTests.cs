using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Tasks;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

public sealed class InventoryTaskListCoordinatorTests
{
    [Fact]
    public async Task EnsureManagedTaskListAsync_creates_the_list_with_the_standing_reminder_item_the_first_time()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        var taskList = await context.TaskRepository.GetByIdAsync(userId, taskListId!.Value, CancellationToken.None);
        Assert.NotNull(taskList);
        Assert.Equal(RestockTaskNaming.TitleFor("Kitchen"), taskList!.Title);
        var reminder = Assert.Single(taskList.Items);
        Assert.Equal(InventoryTaskListCoordinator.UpdateStockReminderDescription, reminder.Description);
        Assert.True(reminder.RemindDaily);
        Assert.Equal(taskListId, await context.ManagedTaskListRepository.GetTaskListIdAsync(inventoryId, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureManagedTaskListAsync_creates_the_list_under_the_inventory_owner_not_the_caller()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId);

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        // Scoped to the owner: a share recipient's own task lists must not gain the owner's restock list.
        Assert.NotNull(await context.TaskRepository.GetByIdAsync(ownerUserId, taskListId!.Value, CancellationToken.None));
        Assert.Null(await context.TaskRepository.GetByIdAsync(Guid.NewGuid(), taskListId.Value, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureManagedTaskListAsync_reuses_the_existing_list_on_a_second_call()
    {
        var context = new InventoryTestContext();
        var inventoryId = context.AddInventory(Guid.NewGuid());

        var firstId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);
        var secondId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        Assert.Equal(firstId, secondId);
    }

    [Fact]
    public async Task EnsureManagedTaskListAsync_creates_a_fresh_list_if_the_tracked_one_was_deleted()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);
        var firstId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);
        await context.TaskRepository.DeleteAsync(userId, firstId!.Value, CancellationToken.None);

        var secondId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        Assert.NotEqual(firstId, secondId);
        Assert.NotNull(await context.TaskRepository.GetByIdAsync(userId, secondId!.Value, CancellationToken.None));
        Assert.Equal(secondId, await context.ManagedTaskListRepository.GetTaskListIdAsync(inventoryId, CancellationToken.None));
    }

    [Fact]
    public async Task EnsureManagedTaskListAsync_returns_null_for_an_inventory_that_no_longer_exists()
    {
        var context = new InventoryTestContext();

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Null(taskListId);
    }

    [Fact]
    public async Task EnsureRestockTaskAsync_is_a_no_op_when_the_item_is_not_below_minimum()
    {
        var context = new InventoryTestContext();
        var inventoryId = context.AddInventory(Guid.NewGuid());
        var item = InventoryItem.Create(inventoryId, "Milk", "Dairy", "Fridge", 5m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);

        var result = await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        Assert.Null(result.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task EnsureRestockTaskAsync_appends_a_restock_item_when_below_minimum()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);
        var item = InventoryItem.Create(inventoryId, "Milk", "Dairy", "Fridge", 0m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);

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
        var inventoryId = context.AddInventory(userId);
        var item = InventoryItem.Create(inventoryId, "Milk", "Dairy", "Fridge", 0m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);
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
        var inventoryId = context.AddInventory(userId);
        var item = InventoryItem.Create(inventoryId, "Milk", "Dairy", "Fridge", 0m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);
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
    /// out. Marking the list High and letting the inventory touch it dropped it back to Normal.
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
        var inventoryId = context.AddInventory(userId);
        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);
        await MarkHighPriorityAsync(context, userId, taskListId!.Value);

        var item = InventoryItem.Create(inventoryId, "Milk", "Dairy", "Fridge", 0m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push);
        await context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);

        var afterwards = await context.TaskRepository.GetByIdAsync(userId, taskListId.Value, CancellationToken.None);
        Assert.Equal(ItemPriority.High, afterwards!.Priority);
    }

    [Fact]
    public async Task Raising_a_shortfall_leaves_the_list_as_important_as_it_was()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);
        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);
        await MarkHighPriorityAsync(context, userId, taskListId!.Value);

        await context.TaskListCoordinator.EnsureShortfallTasksAsync(
            inventoryId, [new RestockNeed("Flour", 5m)], CancellationToken.None);

        var afterwards = await context.TaskRepository.GetByIdAsync(userId, taskListId.Value, CancellationToken.None);
        Assert.Equal(ItemPriority.High, afterwards!.Priority);
    }

    [Fact]
    public async Task Renaming_the_inventory_leaves_its_list_as_important_as_it_was()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);
        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);
        await MarkHighPriorityAsync(context, userId, taskListId!.Value);

        var inventory = await context.InventoryRepository.GetByIdAsync(userId, inventoryId, CancellationToken.None);
        inventory!.Update("Pantry", isPrivate: false, encryptedContent: null);
        await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        var afterwards = await context.TaskRepository.GetByIdAsync(userId, taskListId.Value, CancellationToken.None);
        Assert.Equal(RestockTaskNaming.TitleFor("Pantry"), afterwards!.Title);
        Assert.Equal(ItemPriority.High, afterwards.Priority);
    }

    [Fact]
    public async Task The_standing_reminder_comes_back_at_a_waking_hour()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        var taskList = await context.TaskRepository.GetByIdAsync(userId, taskListId!.Value, CancellationToken.None);
        var reminder = Assert.Single(taskList!.Items);
        // A bare TimeOnly defaults to midnight, which is a reminder nobody is awake to act on.
        Assert.True(reminder.RemindDaily);
        Assert.Equal(new TimeOnly(9, 0), reminder.DailyReminderTimeOfDay);
    }

    [Fact]
    public async Task An_inventory_whose_restock_list_is_switched_off_gets_no_list_at_all()
    {
        var context = new InventoryTestContext();
        var inventoryId = context.AddInventory(Guid.NewGuid());
        await context.ManagedTaskListRepository.SetSettingsAsync(
            inventoryId, RestockListSettings.Default with { IsEnabled = false }, CancellationToken.None);

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        // Null is what every caller already treats as "nowhere to put an errand", so switching the list
        // off needs no new answer anywhere else.
        Assert.Null(taskListId);
        Assert.Null(await context.ManagedTaskListRepository.GetTaskListIdAsync(inventoryId, CancellationToken.None));
    }

    [Fact]
    public async Task The_standing_reminder_carries_a_due_date_so_the_calendar_and_dashboard_can_see_it()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);
        await context.ManagedTaskListRepository.SetSettingsAsync(
            inventoryId, RestockListSettings.Default with { RefreshTimeOfDay = new TimeOnly(7, 30) },
            CancellationToken.None);

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        var taskList = await context.TaskRepository.GetByIdAsync(userId, taskListId!.Value, CancellationToken.None);
        var reminder = Assert.Single(taskList!.Items);
        // Both halves matter: the calendar and the dashboard read entries by their due date and know
        // nothing about a daily reminder, so without one this entry existed only on its own list.
        Assert.NotNull(reminder.DueDateUtc);
        Assert.Equal(new TimeOnly(7, 30), TimeOnly.FromDateTime(reminder.DueDateUtc!.Value.LocalDateTime));
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), DateOnly.FromDateTime(reminder.DueDateUtc.Value.LocalDateTime));
    }

    [Fact]
    public async Task Turning_the_daily_reminder_off_leaves_the_list_without_one_and_without_a_due_date()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);
        await context.ManagedTaskListRepository.SetSettingsAsync(
            inventoryId, RestockListSettings.Default with { RemindDaily = false }, CancellationToken.None);

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        var taskList = await context.TaskRepository.GetByIdAsync(userId, taskListId!.Value, CancellationToken.None);
        var reminder = Assert.Single(taskList!.Items);
        Assert.False(reminder.RemindDaily);
        // No deadline either: a date nobody asked for would only make the entry overdue tomorrow.
        Assert.Null(reminder.DueDateUtc);
    }

    [Fact]
    public async Task The_list_is_created_at_the_priority_the_inventory_asked_for()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);
        await context.ManagedTaskListRepository.SetSettingsAsync(
            inventoryId, RestockListSettings.Default with { ListPriority = ItemPriority.High }, CancellationToken.None);

        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        var taskList = await context.TaskRepository.GetByIdAsync(userId, taskListId!.Value, CancellationToken.None);
        Assert.Equal(ItemPriority.High, taskList!.Priority);
    }

    [Fact]
    public async Task Deleting_the_managed_list_takes_it_away_and_forgets_it()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var inventoryId = context.AddInventory(userId);
        var taskListId = await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        await context.TaskListCoordinator.DeleteManagedTaskListAsync(inventoryId, CancellationToken.None);

        Assert.Null(await context.TaskRepository.GetByIdAsync(userId, taskListId!.Value, CancellationToken.None));
        // Forgetting it is the half that matters for switching back on: a tracking row still pointing at
        // a deleted list is what would make the next Ensure think there was one to reuse.
        Assert.Null(await context.ManagedTaskListRepository.GetTaskListIdAsync(inventoryId, CancellationToken.None));
    }
}

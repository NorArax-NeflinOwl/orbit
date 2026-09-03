using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// Rebuilding an inventory's restock list so it asks for what it should be asking for right now. Which
/// products those are is the inventory's own choice - see RestockListSettings - and that choice cannot
/// be applied one saved item at a time, which is why this exists beside the per-save rule.
/// </summary>
public sealed class RestockListRefreshTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    private RestockListRefresh ARefresh()
        => new(
            _context.ManagedTaskListRepository, _context.InventoryItemRepository, _context.InventoryRepository,
            _context.TaskRepository, _context.TaskListCoordinator);

    [Fact]
    public async Task By_default_the_list_asks_for_whatever_is_below_its_minimum()
    {
        var inventoryId = _context.AddInventory(_userId);
        await AProductAsync(inventoryId, "Flour", quantity: 0, minimum: 5);
        await AProductAsync(inventoryId, "Sugar", quantity: 9, minimum: 4);

        var outcome = await ARefresh().RefreshAsync(inventoryId, CancellationToken.None);

        Assert.Equal(1, outcome.Added);
        Assert.Equal(["Flour"], await ProductsAskedForAsync(inventoryId));
    }

    [Fact]
    public async Task Following_the_plan_asks_only_for_what_a_dated_task_is_waiting_on()
    {
        var inventoryId = _context.AddInventory(_userId);
        var flour = await AProductAsync(inventoryId, "Flour", quantity: 0, minimum: 5);
        await AProductAsync(inventoryId, "Sugar", quantity: 0, minimum: 4);
        await ATaskWaitingOnAsync(flour.Id, dueDateUtc: DateTimeOffset.UtcNow.AddDays(3));
        await FollowThePlanAsync(inventoryId);

        await ARefresh().RefreshAsync(inventoryId, CancellationToken.None);

        // Sugar is below its minimum too, and left off: this list answers "what do I need before
        // Thursday", not "what is running out".
        Assert.Equal(["Flour"], await ProductsAskedForAsync(inventoryId));
    }

    [Fact]
    public async Task A_task_with_no_due_date_is_waiting_for_nothing_in_particular()
    {
        var inventoryId = _context.AddInventory(_userId);
        var flour = await AProductAsync(inventoryId, "Flour", quantity: 0, minimum: 5);
        await ATaskWaitingOnAsync(flour.Id, dueDateUtc: null);
        await FollowThePlanAsync(inventoryId);

        await ARefresh().RefreshAsync(inventoryId, CancellationToken.None);

        // Without a date there is nothing to be early or late for, so there is nothing to shop against.
        Assert.Empty(await ProductsAskedForAsync(inventoryId));
    }

    [Fact]
    public async Task An_errand_the_list_should_no_longer_carry_is_taken_off()
    {
        var inventoryId = _context.AddInventory(_userId);
        var flour = await AProductAsync(inventoryId, "Flour", quantity: 0, minimum: 5);
        await ARefresh().RefreshAsync(inventoryId, CancellationToken.None);
        Assert.Equal(["Flour"], await ProductsAskedForAsync(inventoryId));

        // Somebody counted the shelf and it turns out there is plenty.
        var stocked = await ShelfItemAsync(inventoryId, flour.Id);
        stocked.Update("Flour", "Food", "Dry", 9, 5, InventoryUnit.Piece, null, NotificationChannel.None);
        await _context.InventoryItemRepository.UpdateAsync(stocked, CancellationToken.None);

        var outcome = await ARefresh().RefreshAsync(inventoryId, CancellationToken.None);

        Assert.Equal(1, outcome.Removed);
        Assert.Empty(await ProductsAskedForAsync(inventoryId));
        // And the product stops pointing at an errand that no longer exists.
        Assert.Null((await ShelfItemAsync(inventoryId, flour.Id)).PendingRestockTaskItemId);
    }

    [Fact]
    public async Task Refreshing_twice_asks_for_nothing_the_second_time()
    {
        var inventoryId = _context.AddInventory(_userId);
        await AProductAsync(inventoryId, "Flour", quantity: 0, minimum: 5);
        await ARefresh().RefreshAsync(inventoryId, CancellationToken.None);

        Assert.False((await ARefresh().RefreshAsync(inventoryId, CancellationToken.None)).ChangedAnything);
    }

    [Fact]
    public async Task The_standing_reminder_stays_and_moves_to_the_hour_that_was_chosen()
    {
        var inventoryId = _context.AddInventory(_userId);
        await AProductAsync(inventoryId, "Flour", quantity: 0, minimum: 5);
        await _context.ManagedTaskListRepository.SetSettingsAsync(
            inventoryId, new RestockListSettings(OnlyLinkedWithDueDate: false, new TimeOnly(6, 30)), CancellationToken.None);

        await ARefresh().RefreshAsync(inventoryId, CancellationToken.None);

        var reminder = (await TaskListAsync(inventoryId)).Items
            .Single(item => item.Description == RestockTaskNaming.UpdateStockReminderDescription);
        // A field that changed nothing would look like a field that does nothing.
        Assert.Equal(new TimeOnly(6, 30), reminder.DailyReminderTimeOfDay);
        Assert.True(reminder.RemindDaily);
    }

    [Fact]
    public async Task Anything_somebody_put_on_the_list_themselves_is_left_alone()
    {
        var inventoryId = _context.AddInventory(_userId);
        await AProductAsync(inventoryId, "Flour", quantity: 0, minimum: 5);
        await ARefresh().RefreshAsync(inventoryId, CancellationToken.None);

        var taskList = await TaskListAsync(inventoryId);
        taskList.Update(
            taskList.Title, [.. taskList.Items, TaskItem.Create("Ask about the oven", null, false)],
            taskList.IsGroup, taskList.IsPrivate, taskList.EncryptedContent, taskList.Priority);
        await _context.TaskRepository.UpdateAsync(taskList, CancellationToken.None);

        await ARefresh().RefreshAsync(inventoryId, CancellationToken.None);

        // This maintains the errands Orbit raises, not the list's whole contents.
        Assert.Contains((await TaskListAsync(inventoryId)).Items, item => item.Description == "Ask about the oven");
    }

    private async Task<InventoryItem> AProductAsync(Guid inventoryId, string name, decimal quantity, decimal minimum)
    {
        var item = InventoryItem.Create(
            inventoryId, name, "Food", "Dry", quantity, minimum, InventoryUnit.Piece, null, NotificationChannel.None);
        await _context.InventoryItemRepository.AddAsync(item, CancellationToken.None);
        return item;
    }

    /// <summary>A list somewhere else that wants this product, which is what "following the plan" reads.</summary>
    private async Task ATaskWaitingOnAsync(Guid inventoryItemId, DateTimeOffset? dueDateUtc)
    {
        var taskList = TaskList.Create(
            _userId, "Saturday baking",
            [TaskItem.Create(
                "Flour for the bread", dueDateUtc, isCompleted: false,
                subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: inventoryItemId))]);
        await _context.TaskRepository.AddAsync(taskList, CancellationToken.None);
    }

    private Task FollowThePlanAsync(Guid inventoryId)
        => _context.ManagedTaskListRepository.SetSettingsAsync(
            inventoryId,
            RestockListSettings.Default with { OnlyLinkedWithDueDate = true },
            CancellationToken.None);

    private async Task<TaskList> TaskListAsync(Guid inventoryId)
    {
        var taskListId = await _context.ManagedTaskListRepository.GetTaskListIdAsync(inventoryId, CancellationToken.None);
        return (await _context.TaskRepository.GetByIdAsync(_userId, taskListId!.Value, CancellationToken.None))!;
    }

    private async Task<InventoryItem> ShelfItemAsync(Guid inventoryId, Guid itemId)
        => (await _context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None))
            .Single(item => item.Id == itemId);

    /// <summary>The products the list is currently asking for, by name, so an assertion reads as one.</summary>
    private async Task<IReadOnlyList<string>> ProductsAskedForAsync(Guid inventoryId)
    {
        var taskList = await TaskListAsync(inventoryId);
        var shelf = await _context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None);
        return [.. taskList.Items
            .Where(item => item.Kind == TaskItemKind.Inventory && item.LinkedInventoryItemId is not null)
            .Select(item => shelf.First(product => product.Id == item.LinkedInventoryItemId!.Value).Name)
            .Order()];
    }
}

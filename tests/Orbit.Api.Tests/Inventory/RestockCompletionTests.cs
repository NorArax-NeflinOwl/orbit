using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.FinishRestocking;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

/// <summary>
/// What crossing a restock errand off means to the shelf it came from: somebody went and got the thing,
/// so the amount comes up to the level the shelf is meant to hold.
/// </summary>
public sealed class RestockCompletionTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    /// <summary>A shelf item below its minimum, with the errand that raised for it - the usual starting point.</summary>
    private async Task<(Guid WarehouseId, Guid TaskListId, InventoryItem Item)> ALowItemWithItsErrandAsync(
        decimal quantity = 0, decimal minimum = 5)
    {
        var warehouseId = _context.AddWarehouse(_userId);
        var item = InventoryItem.Create(warehouseId, "Flour", "Food", "Dry", quantity, minimum, null, NotificationChannel.None);
        await _context.InventoryRepository.AddAsync(item, CancellationToken.None);
        var raised = await _context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);
        await _context.InventoryRepository.UpdateAsync(raised, CancellationToken.None);
        return (warehouseId, raised.PendingRestockTaskListId!.Value, raised);
    }

    private RestockCompletion ACompletion()
        => new(_context.ManagedTaskListRepository, _context.InventoryRepository);

    private async Task<InventoryItem> ShelfItemAsync(Guid warehouseId)
        => (await _context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None)).Single();

    [Fact]
    public async Task Crossing_an_errand_off_fills_the_shelf_to_its_minimum()
    {
        var (warehouseId, taskListId, item) = await ALowItemWithItsErrandAsync();

        var toppedUp = await ACompletion().ApplyAsync(taskListId, [item.PendingRestockTaskItemId!.Value], CancellationToken.None);

        Assert.Equal(1, toppedUp);
        Assert.Equal(5, (await ShelfItemAsync(warehouseId)).Quantity);
    }

    [Fact]
    public async Task Somebody_who_stocked_more_than_the_minimum_keeps_it()
    {
        // Finishing an errand is not a claim about how much is there beyond the minimum, so it raises
        // an amount and never lowers one.
        var (warehouseId, taskListId, item) = await ALowItemWithItsErrandAsync();
        var stockedGenerously = await ShelfItemAsync(warehouseId);
        stockedGenerously.Update("Flour", "Food", "Dry", 9, 5, null, NotificationChannel.None);
        await _context.InventoryRepository.UpdateAsync(stockedGenerously, CancellationToken.None);

        var toppedUp = await ACompletion().ApplyAsync(taskListId, [item.PendingRestockTaskItemId!.Value], CancellationToken.None);

        Assert.Equal(0, toppedUp);
        Assert.Equal(9, (await ShelfItemAsync(warehouseId)).Quantity);
    }

    [Fact]
    public async Task An_ordinary_task_list_means_nothing_to_any_shelf()
    {
        var taskList = TaskList.Create(_userId, "Errands", [TaskItem.Create("Buy milk", null, false)]);
        await _context.TaskRepository.AddAsync(taskList, CancellationToken.None);

        var toppedUp = await ACompletion().ApplyAsync(
            taskList.Id, [taskList.Items[0].Id], CancellationToken.None);

        Assert.Equal(0, toppedUp);
    }

    [Fact]
    public async Task Saving_the_list_with_the_errand_ticked_is_what_fills_the_shelf()
    {
        // The whole path, as the checklist screen takes it: a save that happens to cross off an errand.
        var (warehouseId, taskListId, _) = await ALowItemWithItsErrandAsync();
        var taskList = await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None);
        var ticked = taskList!.Items
            .Select(item => TaskItem.FromPersistence(
                item.Id, item.Description, item.DueDateUtc, isCompleted: true, item.LinkedTaskListId,
                item.OverdueNotificationChannel, item.RemindDaily, item.DailyReminderNotificationChannel,
                item.DailyReminderTimeOfDay))
            .ToList();

        var outcome = await new UpdateTaskListCommandHandler(
                new TaskListAccessResolver(_context.TaskRepository, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
                _context.TaskRepository,
                new TaskListLinkValidator(_context.TaskRepository),
                ACompletion())
            .HandleAsync(
                new UpdateTaskListCommand(
                    _userId, taskListId, taskList.Title, ticked, IsGroup: false, IsPrivate: false, EncryptedContent: null),
                CancellationToken.None);

        Assert.Equal(Orbit.Core.Abstractions.EditOutcomeKind.Success, outcome.Kind);
        Assert.Equal(5, (await ShelfItemAsync(warehouseId)).Quantity);
    }

    [Fact]
    public async Task Finishing_the_whole_list_fills_every_shelf_item_and_crosses_the_errands_off()
    {
        var (warehouseId, taskListId, _) = await ALowItemWithItsErrandAsync();
        var second = InventoryItem.Create(warehouseId, "Sugar", "Food", "Dry", 1, 4, null, NotificationChannel.None);
        await _context.InventoryRepository.AddAsync(second, CancellationToken.None);
        await _context.InventoryRepository.UpdateAsync(
            await _context.TaskListCoordinator.EnsureRestockTaskAsync(second, CancellationToken.None), CancellationToken.None);

        var toppedUp = await new FinishRestockingCommandHandler(_context.TaskRepository, ACompletion())
            .HandleAsync(new FinishRestockingCommand(_userId, taskListId), CancellationToken.None);

        Assert.Equal(2, toppedUp);
        var shelf = await _context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None);
        Assert.Equal([4m, 5m], shelf.OrderBy(item => item.Quantity).Select(item => item.Quantity));

        var taskList = await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None);
        Assert.All(
            taskList!.Items.Where(item => RestockTaskNaming.IsRestockEntry(item.Description)),
            item => Assert.True(item.IsCompleted));
    }

    [Fact]
    public async Task Finishing_the_list_leaves_the_standing_reminder_for_the_reader_to_tick()
    {
        // It is crossed off by the tick that asked the question, and comes back tomorrow on its own.
        var (_, taskListId, _) = await ALowItemWithItsErrandAsync();

        await new FinishRestockingCommandHandler(_context.TaskRepository, ACompletion())
            .HandleAsync(new FinishRestockingCommand(_userId, taskListId), CancellationToken.None);

        var taskList = await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None);
        var reminder = Assert.Single(
            taskList!.Items, item => item.Description == RestockTaskNaming.UpdateStockReminderDescription);
        Assert.False(reminder.IsCompleted);
    }
}

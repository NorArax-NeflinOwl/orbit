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
        var item = InventoryItem.Create(warehouseId, "Flour", "Food", "Dry", quantity, minimum, InventoryUnit.Piece, null, NotificationChannel.None);
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
        stockedGenerously.Update("Flour", "Food", "Dry", 9, 5, InventoryUnit.Piece, null, NotificationChannel.None);
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
        var second = InventoryItem.Create(warehouseId, "Sugar", "Food", "Dry", 1, 4, InventoryUnit.Piece, null, NotificationChannel.None);
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
    public async Task Finishing_the_list_finishes_the_standing_reminder_too()
    {
        // The question asked was whether to finish the task; leaving the entry that asked it open would
        // answer it half way, and RemindDaily brings the reminder back tomorrow regardless.
        var (_, taskListId, _) = await ALowItemWithItsErrandAsync();

        await new FinishRestockingCommandHandler(_context.TaskRepository, ACompletion())
            .HandleAsync(new FinishRestockingCommand(_userId, taskListId), CancellationToken.None);

        var taskList = await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None);
        Assert.All(taskList!.Items, item => Assert.True(item.IsCompleted));
    }

    /// <summary>
    /// A change nothing else can hear about might as well not have happened. Crossing the entries off
    /// left the list's own timestamp where it was, so the change feed never mentioned it again: the
    /// browser that finished the round re-read the list itself and looked right, while a phone showed
    /// the round still outstanding for good - see TaskList.CompleteEverything.
    /// </summary>
    [Fact]
    public async Task Finishing_the_list_says_the_list_changed()
    {
        var (_, taskListId, _) = await ALowItemWithItsErrandAsync();
        var before = (await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None))!
            .UpdatedAtUtc;

        await new FinishRestockingCommandHandler(_context.TaskRepository, ACompletion())
            .HandleAsync(new FinishRestockingCommand(_userId, taskListId), CancellationToken.None);

        var taskList = await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None);
        Assert.True(taskList!.UpdatedAtUtc > before);
        // And the list itself now reads as done, which is what the tasks page sorts and counts by.
        Assert.True(taskList.IsCompleted);
    }

    /// <summary>Nothing to cross off is not a change, and should not look like one to anybody syncing.</summary>
    [Fact]
    public async Task Finishing_a_list_already_done_leaves_its_timestamp_alone()
    {
        var (_, taskListId, _) = await ALowItemWithItsErrandAsync();
        var handler = new FinishRestockingCommandHandler(_context.TaskRepository, ACompletion());
        await handler.HandleAsync(new FinishRestockingCommand(_userId, taskListId), CancellationToken.None);
        var afterFirst = (await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None))!
            .UpdatedAtUtc;

        await handler.HandleAsync(new FinishRestockingCommand(_userId, taskListId), CancellationToken.None);

        var taskList = await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None);
        Assert.Equal(afterFirst, taskList!.UpdatedAtUtc);
    }
}

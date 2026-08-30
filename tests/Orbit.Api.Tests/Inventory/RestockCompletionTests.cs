using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.FinishRestocking;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

/// <summary>
/// What crossing a restock errand off means: somebody went and got the thing, so the shelf comes up to
/// the level it is meant to hold - and the errand leaves the list, because it is no longer something
/// missing. A list of permanently crossed-off lines is a list that stops being read.
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

    private RestockCompletion ACompletion() => _context.RestockCompletion;

    private async Task<TaskList> TaskListAsync(Guid taskListId)
        => (await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None))!;

    /// <summary>Crosses off everything on the list, the way a save from the checklist screen arrives.</summary>
    private async Task TickEverythingAsync(Guid taskListId)
    {
        var taskList = await TaskListAsync(taskListId);
        foreach (var item in taskList.Items)
        {
            item.Complete();
        }

        await _context.TaskRepository.UpdateAsync(taskList, CancellationToken.None);
    }

    private async Task<InventoryItem> ShelfItemAsync(Guid warehouseId)
        => (await _context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None)).Single();

    [Fact]
    public async Task Crossing_an_errand_off_fills_the_shelf_to_its_minimum()
    {
        var (warehouseId, taskListId, _) = await ALowItemWithItsErrandAsync();
        await TickEverythingAsync(taskListId);

        var outcome = await ACompletion().ReconcileAsync(taskListId, CancellationToken.None);

        Assert.Equal(1, outcome.ToppedUp);
        Assert.Equal(5, (await ShelfItemAsync(warehouseId)).Quantity);
    }

    [Fact]
    public async Task A_settled_errand_leaves_the_list()
    {
        var (_, taskListId, _) = await ALowItemWithItsErrandAsync();
        await TickEverythingAsync(taskListId);

        await ACompletion().ReconcileAsync(taskListId, CancellationToken.None);

        // The standing reminder stays - crossing that off is a claim about the whole shelf, and
        // RemindDaily brings it back tomorrow rather than it leaving.
        var remaining = await TaskListAsync(taskListId);
        Assert.DoesNotContain(remaining.Items, item => RestockTaskNaming.IsRestockEntry(item.Description));
        Assert.Contains(remaining.Items, item => item.Description == RestockTaskNaming.UpdateStockReminderDescription);
    }

    [Fact]
    public async Task The_shelf_item_stops_pointing_at_an_errand_that_no_longer_exists()
    {
        var (warehouseId, taskListId, _) = await ALowItemWithItsErrandAsync();
        await TickEverythingAsync(taskListId);

        await ACompletion().ReconcileAsync(taskListId, CancellationToken.None);

        // Left dangling, the next time this product went low EnsureRestockTaskAsync would look up an
        // entry that had been removed - and throw rather than raise a new errand.
        var shelfItem = await ShelfItemAsync(warehouseId);
        Assert.Null(shelfItem.PendingRestockTaskItemId);
        Assert.Null(shelfItem.PendingRestockTaskListId);
    }

    [Fact]
    public async Task An_errand_settled_once_is_not_settled_again()
    {
        var (_, taskListId, _) = await ALowItemWithItsErrandAsync();
        await TickEverythingAsync(taskListId);
        await ACompletion().ReconcileAsync(taskListId, CancellationToken.None);

        // Reconciling runs both on save and on opening the list, so running twice has to be worth
        // nothing the second time.
        var second = await ACompletion().ReconcileAsync(taskListId, CancellationToken.None);

        Assert.False(second.ChangedAnything);
    }

    [Fact]
    public async Task An_errand_written_before_the_link_existed_is_still_settled()
    {
        // Entries created before TaskItemKind.Inventory carry no link, only "Restock: Flour (5)". They
        // are matched by the product name in their own description, which is what lets a list that has
        // been accumulating crossed-off errands settle the first time it is opened.
        var (warehouseId, taskListId, _) = await ALowItemWithItsErrandAsync();
        var taskList = await TaskListAsync(taskListId);
        var withoutLinks = taskList.Items
            .Select(item => TaskItem.FromPersistence(
                item.Id, item.Description, item.DueDateUtc, isCompleted: true, item.LinkedTaskListId,
                item.OverdueNotificationChannel, item.RemindDaily, item.DailyReminderNotificationChannel,
                item.DailyReminderTimeOfDay))
            .ToList();
        taskList.Update(taskList.Title, withoutLinks, taskList.IsGroup, taskList.IsPrivate, taskList.EncryptedContent, taskList.Priority);
        await _context.TaskRepository.UpdateAsync(taskList, CancellationToken.None);

        var outcome = await ACompletion().ReconcileAsync(taskListId, CancellationToken.None);

        Assert.Equal(1, outcome.ToppedUp);
        Assert.Equal(5, (await ShelfItemAsync(warehouseId)).Quantity);
        Assert.DoesNotContain((await TaskListAsync(taskListId)).Items, item => RestockTaskNaming.IsRestockEntry(item.Description));
    }

    [Fact]
    public async Task An_errand_for_a_product_that_has_since_been_deleted_still_leaves_the_list()
    {
        var (warehouseId, taskListId, _) = await ALowItemWithItsErrandAsync();
        var shelfItem = await ShelfItemAsync(warehouseId);
        await _context.InventoryRepository.DeleteAsync(warehouseId, shelfItem.Id, CancellationToken.None);
        await TickEverythingAsync(taskListId);

        await ACompletion().ReconcileAsync(taskListId, CancellationToken.None);

        // There is nothing left to bring back, so the errand is over. Keeping it would leave a crossed-off
        // line about a product that no longer exists, for ever.
        Assert.DoesNotContain((await TaskListAsync(taskListId)).Items, item => RestockTaskNaming.IsRestockEntry(item.Description));
    }

    [Fact]
    public async Task Somebody_who_stocked_more_than_the_minimum_keeps_it()
    {
        // Finishing an errand is not a claim about how much is there beyond the minimum, so it raises
        // an amount and never lowers one.
        var (warehouseId, taskListId, _) = await ALowItemWithItsErrandAsync();
        var stockedGenerously = await ShelfItemAsync(warehouseId);
        stockedGenerously.Update("Flour", "Food", "Dry", 9, 5, InventoryUnit.Piece, null, NotificationChannel.None);
        await _context.InventoryRepository.UpdateAsync(stockedGenerously, CancellationToken.None);
        await TickEverythingAsync(taskListId);

        var outcome = await ACompletion().ReconcileAsync(taskListId, CancellationToken.None);

        Assert.Equal(0, outcome.ToppedUp);
        Assert.Equal(9, (await ShelfItemAsync(warehouseId)).Quantity);
    }

    [Fact]
    public async Task An_ordinary_task_list_means_nothing_to_any_shelf()
    {
        var taskList = TaskList.Create(_userId, "Errands", [TaskItem.Create("Buy milk", null, isCompleted: true)]);
        await _context.TaskRepository.AddAsync(taskList, CancellationToken.None);

        var outcome = await ACompletion().ReconcileAsync(taskList.Id, CancellationToken.None);

        Assert.False(outcome.ChangedAnything);
        // And nothing was taken off it: a crossed-off entry on a list nobody manages is the reader's.
        Assert.Single((await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None))!.Items);
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
        // And the same save took the finished errand off the list.
        Assert.DoesNotContain((await TaskListAsync(taskListId)).Items, item => RestockTaskNaming.IsRestockEntry(item.Description));
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

        // Finished means gone, not crossed off: everything the list was asking for is now on the shelf.
        Assert.DoesNotContain((await TaskListAsync(taskListId)).Items, item => RestockTaskNaming.IsRestockEntry(item.Description));
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
        // Aged first - see InMemoryTaskRepository.PretendItWasLastChanged. Raising the errand stamped the
        // list a moment ago, and finishing it below could tie with that stamp rather than beat it.
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        _context.TaskRepository.PretendItWasLastChanged(taskListId, before);

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

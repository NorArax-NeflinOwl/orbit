using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// What ticking a product entry does to the shelf behind it, as the user gave the rule on 2026-09-19:
/// the entry's own minimum is how much that piece of work needs, ticking it puts that much on the shelf,
/// unticking takes the same back off, and crossing it off does neither.
///
/// Driven through the real save wherever the point is the tick itself, because that is where the state
/// each entry keeps with the shelf is carried forward (see TaskItem.Stock) - a rule tested only against
/// StockedEntryStock's own method would pass while every save re-added the same amount.
/// </summary>
public sealed class StockedEntryStockTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    /// <summary>A row on a shelf of this reader's, with the count everything here is about.</summary>
    private async Task<InventoryItem> AShelfItemAsync(decimal quantity, decimal? minimumQuantity)
    {
        var inventoryId = _context.AddInventory(_userId);
        var item = InventoryItem.Create(
            inventoryId, "Zupka Buldog", "Food", ["Dry goods"], quantity, minimumQuantity,
            InventoryUnit.Piece, expiryDate: null, NotificationChannel.None, position: 0);
        await _context.InventoryItemRepository.AddAsync(item, CancellationToken.None);
        return item;
    }

    /// <summary>An entry standing for a row on a shelf, needing as much of it as it says.</summary>
    private static TaskItem StandingFor(
        InventoryItem shelfItem, decimal? requiredQuantity, bool isCompleted = false, bool isFailed = false)
        => TaskItem.Create(
            shelfItem.Name, dueDateUtc: null, isCompleted, isFailed: isFailed,
            subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: shelfItem.Id),
            requiredQuantity: requiredQuantity);

    /// <summary>The count on the shelf as it is stored, which is the answer every test here checks.</summary>
    private async Task<decimal> StockOfAsync(InventoryItem shelfItem)
        => (await _context.InventoryItemRepository.GetByIdsAsync([shelfItem.Id], CancellationToken.None))
            .Single().Quantity;

    private async Task<TaskList> AListAsync(params TaskItem[] items)
    {
        var taskList = TaskList.Create(_userId, "Zakupy", [.. items]);
        await _context.TaskRepository.AddAsync(taskList, CancellationToken.None);
        return taskList;
    }

    /// <summary>The save every client makes, with the entries as they now stand.</summary>
    private async Task<TaskList> SaveAsync(TaskList taskList, params TaskItem[] items)
    {
        var outcome = await new UpdateTaskListCommandHandler(
                new TaskListAccessResolver(
                    _context.TaskRepository, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
                _context.TaskRepository,
                new TaskListLinkValidator(_context.TaskRepository),
                _context.RestockCompletion,
                _context.StockedEntryCompletion,
                _context.StockedEntryStock,
                _context.ProductEntryPlacement)
            .HandleAsync(
                new UpdateTaskListCommand(
                    _userId, taskList.Id, taskList.Title, items, IsGroup: false, IsPrivate: false,
                    EncryptedContent: null),
                CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        return (await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None))!;
    }

    /// <summary>The same entry as stored, ticked, crossed off, or put back to nothing.</summary>
    private static TaskItem AsAnswered(TaskItem stored, bool isCompleted, bool isFailed = false)
        => TaskItem.FromPersistence(
            stored.Id, stored.Description, stored.DueDateUtc, isCompleted, stored.LinkedTaskListIds,
            stored.Reminders, stored.Subject, stored.Categories, stored.Product, stored.Notes, isFailed,
            requiredQuantity: stored.RequiredQuantity);

    [Fact]
    public async Task Ticking_an_entry_puts_its_own_minimum_on_the_shelf()
    {
        var shelfItem = await AShelfItemAsync(quantity: 0, minimumQuantity: 10);
        var taskList = await AListAsync(StandingFor(shelfItem, requiredQuantity: 2));
        var entry = taskList.Items[0];

        await SaveAsync(taskList, AsAnswered(entry, isCompleted: true));

        Assert.Equal(2, await StockOfAsync(shelfItem));
    }

    /// <summary>
    /// The entry's own minimum, never the shelf's. The two differ the moment a second list asks for the
    /// same product: the shelf is kept at what they add up to, and a tick is a claim about one of them.
    /// </summary>
    [Fact]
    public async Task Two_lists_asking_for_the_same_thing_each_put_on_their_own_share()
    {
        var shelfItem = await AShelfItemAsync(quantity: 0, minimumQuantity: null);
        var first = await AListAsync(StandingFor(shelfItem, requiredQuantity: 2));
        var second = await AListAsync(StandingFor(shelfItem, requiredQuantity: 3));

        await SaveAsync(first, AsAnswered(first.Items[0], isCompleted: true));
        Assert.Equal(2, await StockOfAsync(shelfItem));

        await SaveAsync(second, AsAnswered(second.Items[0], isCompleted: true));
        Assert.Equal(5, await StockOfAsync(shelfItem));
    }

    /// <summary>Saving the same tick again moves nothing: the entry already says its share is on the shelf.</summary>
    [Fact]
    public async Task Saving_the_same_tick_twice_puts_nothing_on_twice()
    {
        var shelfItem = await AShelfItemAsync(quantity: 0, minimumQuantity: 10);
        var taskList = await AListAsync(StandingFor(shelfItem, requiredQuantity: 2));

        var saved = await SaveAsync(taskList, AsAnswered(taskList.Items[0], isCompleted: true));
        await SaveAsync(saved, AsAnswered(saved.Items[0], isCompleted: true));

        Assert.Equal(2, await StockOfAsync(shelfItem));
    }

    [Fact]
    public async Task Unticking_an_entry_takes_its_minimum_back_off()
    {
        var shelfItem = await AShelfItemAsync(quantity: 0, minimumQuantity: 10);
        var taskList = await AListAsync(StandingFor(shelfItem, requiredQuantity: 2));

        var saved = await SaveAsync(taskList, AsAnswered(taskList.Items[0], isCompleted: true));
        await SaveAsync(saved, AsAnswered(saved.Items[0], isCompleted: false));

        Assert.Equal(0, await StockOfAsync(shelfItem));
    }

    /// <summary>
    /// Crossing an entry off does neither: giving up on something after having got it does not unbuy it.
    /// The press after that - back to nothing - is the untick, and that is what takes the amount off.
    /// </summary>
    [Fact]
    public async Task Crossing_a_ticked_entry_off_leaves_the_shelf_alone_and_the_press_after_it_does_not()
    {
        var shelfItem = await AShelfItemAsync(quantity: 0, minimumQuantity: 10);
        var taskList = await AListAsync(StandingFor(shelfItem, requiredQuantity: 2));

        var ticked = await SaveAsync(taskList, AsAnswered(taskList.Items[0], isCompleted: true));
        var crossedOff = await SaveAsync(ticked, AsAnswered(ticked.Items[0], isCompleted: false, isFailed: true));
        Assert.Equal(2, await StockOfAsync(shelfItem));

        await SaveAsync(crossedOff, AsAnswered(crossedOff.Items[0], isCompleted: false));

        Assert.Equal(0, await StockOfAsync(shelfItem));
    }

    /// <summary>And crossing off something never ticked puts nothing anywhere.</summary>
    [Fact]
    public async Task Crossing_off_an_entry_nobody_ticked_puts_nothing_on_the_shelf()
    {
        var shelfItem = await AShelfItemAsync(quantity: 0, minimumQuantity: 10);
        var taskList = await AListAsync(StandingFor(shelfItem, requiredQuantity: 2));

        await SaveAsync(taskList, AsAnswered(taskList.Items[0], isCompleted: false, isFailed: true));

        Assert.Equal(0, await StockOfAsync(shelfItem));
    }

    /// <summary>
    /// An entry the shelf crossed off puts nothing there: nobody got anything, the shelf simply already
    /// held enough. Counting the two together would double what the shelf says it has.
    /// </summary>
    [Fact]
    public async Task An_entry_the_shelf_crossed_off_adds_nothing_to_it()
    {
        var shelfItem = await AShelfItemAsync(quantity: 4, minimumQuantity: 2);
        var taskList = await AListAsync(StandingFor(shelfItem, requiredQuantity: 2));

        var saved = await SaveAsync(taskList, taskList.Items[0]);

        Assert.True(saved.Items[0].IsCompleted);
        Assert.Equal(4, await StockOfAsync(shelfItem));
    }

    /// <summary>An entry with no minimum of its own is worth one, the way everything else counts it.</summary>
    [Fact]
    public async Task An_entry_that_says_no_amount_is_worth_one()
    {
        var shelfItem = await AShelfItemAsync(quantity: 0, minimumQuantity: 10);
        var taskList = await AListAsync(StandingFor(shelfItem, requiredQuantity: null));

        await SaveAsync(taskList, AsAnswered(taskList.Items[0], isCompleted: true));

        Assert.Equal(1, await StockOfAsync(shelfItem));
    }

    /// <summary>
    /// Never below nothing: an entry unticked after the shelf was emptied by hand has nothing left to
    /// take back, and a shelf holding minus one of something is a number nobody can act on.
    /// </summary>
    [Fact]
    public async Task Unticking_never_takes_the_shelf_below_nothing()
    {
        var shelfItem = await AShelfItemAsync(quantity: 0, minimumQuantity: 10);
        var taskList = await AListAsync(StandingFor(shelfItem, requiredQuantity: 2));
        var ticked = await SaveAsync(taskList, AsAnswered(taskList.Items[0], isCompleted: true));

        // Emptied on the shelf itself, the way counting it down to nothing does.
        var onTheShelf = (await _context.InventoryItemRepository.GetByIdsAsync([shelfItem.Id], CancellationToken.None))
            .Single();
        onTheShelf.MoveStockBy(-onTheShelf.Quantity);
        await _context.InventoryItemRepository.UpdateAsync(onTheShelf, CancellationToken.None);

        await SaveAsync(ticked, AsAnswered(ticked.Items[0], isCompleted: false));

        Assert.Equal(0, await StockOfAsync(shelfItem));
    }
}

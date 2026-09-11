using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// A product entry saved on a list measured against a shelf goes onto that shelf and stands for its row
/// - see ProductEntryPlacement. Exercised through the list's own save, which is the one path every
/// client takes.
/// </summary>
public sealed class ProductEntryPlacementTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    private static TaskItem Asking(string description, TaskItemProduct? product = null)
        => TaskItem.Create(
            description, dueDateUtc: null, isCompleted: false,
            subject: new TaskItemSubject(TaskItemKind.Inventory), product: product ?? TaskItemProduct.Default);

    private static TaskItemProduct Wanting(decimal? minimum = null, decimal quantity = 0)
        => TaskItemProduct.Default with { MinimumQuantity = minimum, Quantity = quantity };

    private async Task<TaskList> AListAsync(Guid? inventoryId)
    {
        var taskList = TaskList.Create(_userId, "Zakupy", []);
        taskList.LinkToInventory(inventoryId);
        await _context.TaskRepository.AddAsync(taskList, CancellationToken.None);
        return taskList;
    }

    private async Task<EditOutcome> SaveAsync(TaskList taskList, params TaskItem[] items)
        => await new UpdateTaskListCommandHandler(
                new TaskListAccessResolver(
                    _context.TaskRepository, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
                _context.TaskRepository,
                new TaskListLinkValidator(_context.TaskRepository),
                _context.RestockCompletion,
                _context.StockedEntryCompletion,
                _context.ProductEntryPlacement)
            .HandleAsync(
                new UpdateTaskListCommand(
                    _userId, taskList.Id, taskList.Title, items, IsGroup: false, IsPrivate: false,
                    EncryptedContent: null),
                CancellationToken.None);

    private async Task<TaskList> StoredAsync(Guid taskListId)
        => (await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None))!;

    private Task<IReadOnlyList<InventoryItem>> ShelfAsync(Guid inventoryId)
        => _context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None);

    private async Task<InventoryItem> ARowAsync(Guid inventoryId, string name, decimal quantity, decimal? minimum)
    {
        var row = InventoryItem.Create(
            inventoryId, name, "Part", [], quantity, minimum, InventoryUnit.Piece, expiryDate: null,
            NotificationChannel.None);
        await _context.InventoryItemRepository.AddAsync(row, CancellationToken.None);
        return row;
    }

    [Fact]
    public async Task A_product_entry_saved_on_a_linked_list_is_put_on_its_shelf()
    {
        var inventoryId = _context.AddInventory(_userId);
        var taskList = await AListAsync(inventoryId);

        var outcome = await SaveAsync(
            taskList,
            Asking("Mąka", TaskItemProduct.Default with
            {
                MinimumQuantity = 2, Unit = InventoryUnit.Kilogram, ProductType = "Dry goods"
            }),
            TaskItem.Create("Jajka", dueDateUtc: null, isCompleted: false));

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var row = Assert.Single(await ShelfAsync(inventoryId));
        Assert.Equal("Mąka", row.Name);
        Assert.Equal(2, row.MinimumQuantity);
        Assert.Equal(0, row.Quantity);
        Assert.Equal(InventoryUnit.Kilogram, row.Unit);
        Assert.Equal("Dry goods", row.ProductType);

        var stored = await StoredAsync(taskList.Id);
        var flour = Assert.Single(stored.Items, item => item.Description == "Mąka");
        Assert.Equal(row.Id, flour.LinkedInventoryItemId);
        // The row is the answer now, so the entry's own copy is gone - see TaskItemProduct.
        Assert.Null(flour.Product);
        // An ordinary line is work, not a product, and goes nowhere.
        Assert.Null(Assert.Single(stored.Items, item => item.Description == "Jajka").LinkedInventoryItemId);
    }

    /// <summary>Duplicates in one save are one row, counted by the same rule a generated shelf is.</summary>
    [Fact]
    public async Task Entries_naming_one_thing_become_one_row_with_their_minimums_added_up()
    {
        var inventoryId = _context.AddInventory(_userId);
        var taskList = await AListAsync(inventoryId);

        await SaveAsync(
            taskList, Asking("Mąka", Wanting(minimum: 2, quantity: 4)), Asking(" mąka", Wanting(minimum: 3, quantity: 1)));

        var row = Assert.Single(await ShelfAsync(inventoryId));
        Assert.Equal(5, row.MinimumQuantity);
        Assert.Equal(1, row.Quantity);
        Assert.All((await StoredAsync(taskList.Id)).Items, entry => Assert.Equal(row.Id, entry.LinkedInventoryItemId));
    }

    /// <summary>
    /// A shelf already holding the thing is what the entry was asking for: it is pointed at, not added
    /// again - and not changed, since a reused list would otherwise raise its minimum every trip.
    /// </summary>
    [Fact]
    public async Task A_row_already_holding_the_thing_is_what_the_entry_stands_for()
    {
        var inventoryId = _context.AddInventory(_userId);
        var existing = await ARowAsync(inventoryId, "mąka", quantity: 7, minimum: 1);
        var taskList = await AListAsync(inventoryId);

        await SaveAsync(taskList, Asking(" MĄKA ", Wanting(minimum: 3)));

        var row = Assert.Single(await ShelfAsync(inventoryId));
        Assert.Equal(existing.Id, row.Id);
        Assert.Equal(1, row.MinimumQuantity);
        Assert.Equal(7, row.Quantity);
        Assert.Equal(existing.Id, Assert.Single((await StoredAsync(taskList.Id)).Items).LinkedInventoryItemId);
    }

    /// <summary>Two rows of one name give no answer to "which one", so the entry is left as it was.</summary>
    [Fact]
    public async Task Two_rows_of_one_name_leave_the_entry_describing_what_it_wants()
    {
        var inventoryId = _context.AddInventory(_userId);
        await ARowAsync(inventoryId, "Mąka", quantity: 1, minimum: 1);
        await ARowAsync(inventoryId, "Mąka", quantity: 2, minimum: 1);
        var taskList = await AListAsync(inventoryId);

        await SaveAsync(taskList, Asking("Mąka", Wanting(minimum: 3)));

        Assert.Equal(2, (await ShelfAsync(inventoryId)).Count);
        var entry = Assert.Single((await StoredAsync(taskList.Id)).Items);
        Assert.Null(entry.LinkedInventoryItemId);
        Assert.Equal(3, entry.Product!.MinimumQuantity);
    }

    /// <summary>A list measured against no shelf has nowhere to put anything; its entries keep their description.</summary>
    [Fact]
    public async Task A_product_entry_on_a_list_with_no_shelf_is_left_as_it_was()
    {
        var inventoryId = _context.AddInventory(_userId);
        var taskList = await AListAsync(inventoryId: null);

        await SaveAsync(taskList, Asking("Mąka", Wanting(minimum: 2)));

        Assert.Empty(await ShelfAsync(inventoryId));
        var entry = Assert.Single((await StoredAsync(taskList.Id)).Items);
        Assert.Null(entry.LinkedInventoryItemId);
        Assert.Equal(2, entry.Product!.MinimumQuantity);
    }

    /// <summary>A row that already holds what its entry asked for crosses the entry off in the same save.</summary>
    [Fact]
    public async Task An_entry_its_new_row_already_covers_is_crossed_off()
    {
        var inventoryId = _context.AddInventory(_userId);
        var taskList = await AListAsync(inventoryId);

        await SaveAsync(taskList, Asking("Zupka Buldog", Wanting(minimum: 2, quantity: 4)));

        Assert.True(Assert.Single((await StoredAsync(taskList.Id)).Items).IsCompleted);
    }

    /// <summary>A new row short of its minimum is asked for on the restock list straight away.</summary>
    [Fact]
    public async Task A_row_that_arrives_short_is_asked_for_on_the_restock_list()
    {
        var inventoryId = _context.AddInventory(_userId);
        var taskList = await AListAsync(inventoryId);

        await SaveAsync(taskList, Asking("Mąka", Wanting(minimum: 2)));

        var row = Assert.Single(await ShelfAsync(inventoryId));
        var restockListId = await _context.ManagedTaskListRepository.GetTaskListIdAsync(inventoryId, CancellationToken.None);
        var restockList = await StoredAsync(restockListId!.Value);
        Assert.Contains(restockList.Items, item => item.LinkedInventoryItemId == row.Id);
    }

    /// <summary>
    /// A client that sends the entry as it was before its last save was placed - a tab saved twice, a
    /// phone that pushed before it pulled - does not put the thing on the shelf a second time.
    /// </summary>
    [Fact]
    public async Task An_entry_sent_again_as_it_was_before_it_was_placed_is_not_placed_twice()
    {
        var inventoryId = _context.AddInventory(_userId);
        var taskList = await AListAsync(inventoryId);
        var entry = Asking("Mąka", Wanting(minimum: 2));
        await SaveAsync(taskList, entry);
        var row = Assert.Single(await ShelfAsync(inventoryId));

        var asTheClientStillHasIt = TaskItem.FromPersistence(
            entry.Id, "Mąka", dueDateUtc: null, isCompleted: false, linkedTaskListIds: null, TaskItemReminders.Default,
            new TaskItemSubject(TaskItemKind.Inventory), product: Wanting(minimum: 2));
        await SaveAsync(taskList, asTheClientStillHasIt);

        Assert.Equal(row.Id, Assert.Single(await ShelfAsync(inventoryId)).Id);
        Assert.Equal(row.Id, Assert.Single((await StoredAsync(taskList.Id)).Items).LinkedInventoryItemId);
        Assert.Equal(2, row.MinimumQuantity);
    }

    /// <summary>
    /// A new row is filed under what its product says, and failing that under what its entry says - the
    /// browser's and the phone's forms ask once, on the entry, and send that answer on the product.
    /// </summary>
    [Fact]
    public async Task A_new_row_is_filed_under_what_its_entry_described()
    {
        var inventoryId = _context.AddInventory(_userId);
        var taskList = await AListAsync(inventoryId);

        await SaveAsync(
            taskList,
            Asking("Kawa", TaskItemProduct.Default with { Categories = ["food", "drinks"] }),
            TaskItem.Create(
                "Herbata", dueDateUtc: null, isCompleted: false,
                subject: new TaskItemSubject(TaskItemKind.Inventory), categories: ["drinks"]));

        var shelf = await ShelfAsync(inventoryId);
        Assert.Equal(["food", "drinks"], Assert.Single(shelf, row => row.Name == "Kawa").Categories);
        Assert.Equal(["drinks"], Assert.Single(shelf, row => row.Name == "Herbata").Categories);
    }
}

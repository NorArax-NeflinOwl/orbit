using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.UpdateInventory;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// The other direction of ShelfUsageTests: an amount somebody changed on the shelf, carried back to the
/// entries asking for it. The user's rule, 2026-09-18 - "every update on the list should update the
/// inventory and the other way round, and where one item is used by more than one list, warn: the reader
/// changes the list themselves, or chooses Split evenly".
/// </summary>
public sealed class ShelfDemandTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    private InventoryItem AShelfItem(Guid inventoryId, decimal? minimum = null, string name = "Flour")
    {
        var item = InventoryItem.Create(
            inventoryId, name, string.Empty, null, quantity: 0, minimum, InventoryUnit.Kilogram, null,
            NotificationChannel.None);
        _context.InventoryItemRepository.AddAsync(item, CancellationToken.None).GetAwaiter().GetResult();
        return item;
    }

    private static TaskItem AnEntryFor(InventoryItem item, decimal? needs)
        => TaskItem.Create(
            item.Name, null, false, subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: item.Id),
            requiredQuantity: needs);

    private async Task<TaskList> AListAsync(string title, params TaskItem[] items)
    {
        var taskList = TaskList.Create(UserId, title, items);
        await _context.TaskRepository.AddAsync(taskList, CancellationToken.None);
        return taskList;
    }

    /// <summary>A save of the shelf, as the endpoint builds one: the whole item list, and nothing else said.</summary>
    private Task<Orbit.Core.Abstractions.EditOutcome> SaveTheShelfAsync(
        Guid inventoryId, IReadOnlyList<InventoryItemInput> items, IReadOnlyList<Guid>? splitEvenlyAcross = null)
        => _context.InventorySave().HandleAsync(
            new UpdateInventoryCommand(
                UserId, inventoryId, "Kitchen", items, IsPrivate: false, EncryptedContent: null,
                SplitEvenlyAcross: splitEvenlyAcross),
            CancellationToken.None);

    private static InventoryItemInput Row(InventoryItem item, decimal? minimum)
        => new(
            item.Id, item.Name, item.ProductType, item.Categories, item.Quantity, minimum, item.Unit,
            item.ExpiryDate, item.ExpiryNotificationChannel);

    [Fact]
    public async Task It_says_which_entries_ask_for_a_shelf_item()
    {
        var inventoryId = _context.AddInventory(UserId);
        var flour = AShelfItem(inventoryId);
        await AListAsync("Bread", AnEntryFor(flour, 2));
        await AListAsync("Pizza", AnEntryFor(flour, needs: null));

        var claims = await _context.ShelfDemand.WhoAsksForAsync(
            UserId, new HashSet<Guid> { flour.Id }, CancellationToken.None);

        Assert.Equal(["Bread", "Pizza"], claims.Select(claim => claim.TaskListName).Order());
        // One for the entry that says nothing - the same rule ShelfUsage counts by.
        Assert.Equal([1, 2], claims.Select(claim => claim.Quantity).Order());
    }

    /// <summary>The whole point: the shelf is edited, and the one list asking for it is told.</summary>
    [Fact]
    public async Task Changing_a_minimum_only_one_list_asks_for_changes_that_list()
    {
        var inventoryId = _context.AddInventory(UserId);
        var flour = AShelfItem(inventoryId, minimum: 2);
        var entry = AnEntryFor(flour, 2);
        var bread = await AListAsync("Bread", entry);

        await SaveTheShelfAsync(inventoryId, [Row(flour, minimum: 6)]);

        Assert.Equal(6, bread.Items.Single(item => item.Id == entry.Id).RequiredQuantity);
        // And the count on the shelf follows, so the two never disagree - see ShelfUsage.
        Assert.Equal(6, flour.Usage);
    }

    /// <summary>
    /// Two lists asking, and nothing said about how to divide it: both are left exactly as they were.
    /// The reader was warned and answered "I'll change the lists myself" - see InventoryEditor.
    /// </summary>
    [Fact]
    public async Task A_minimum_two_lists_ask_for_is_not_divided_on_its_own()
    {
        var inventoryId = _context.AddInventory(UserId);
        var flour = AShelfItem(inventoryId, minimum: 2);
        var breadEntry = AnEntryFor(flour, 2);
        var pizzaEntry = AnEntryFor(flour, 1);
        var bread = await AListAsync("Bread", breadEntry);
        var pizza = await AListAsync("Pizza", pizzaEntry);

        await SaveTheShelfAsync(inventoryId, [Row(flour, minimum: 9)]);

        Assert.Equal(2, bread.Items.Single().RequiredQuantity);
        Assert.Equal(1, pizza.Items.Single().RequiredQuantity);
    }

    /// <summary>And the other answer: divide it equally between everything asking.</summary>
    [Fact]
    public async Task Split_evenly_divides_the_new_amount_between_the_entries_asking()
    {
        var inventoryId = _context.AddInventory(UserId);
        var flour = AShelfItem(inventoryId, minimum: 2);
        var bread = await AListAsync("Bread", AnEntryFor(flour, 2));
        var pizza = await AListAsync("Pizza", AnEntryFor(flour, 1));

        await SaveTheShelfAsync(inventoryId, [Row(flour, minimum: 9)], splitEvenlyAcross: [flour.Id]);

        Assert.Equal(4.5m, bread.Items.Single().RequiredQuantity);
        Assert.Equal(4.5m, pizza.Items.Single().RequiredQuantity);
        Assert.Equal(9, flour.Usage);
    }

    /// <summary>
    /// A division that does not come out even still adds up to what was typed: the shelf and the lists
    /// must agree to the penny, or the next save would show a number nobody chose - see ShelfDemand.SharesOf.
    /// </summary>
    [Fact]
    public async Task An_uneven_split_still_adds_up_to_what_was_typed()
    {
        var inventoryId = _context.AddInventory(UserId);
        var flour = AShelfItem(inventoryId, minimum: 1);
        var bread = await AListAsync("Bread", AnEntryFor(flour, 1));
        var pizza = await AListAsync("Pizza", AnEntryFor(flour, 1));
        var pasta = await AListAsync("Pasta", AnEntryFor(flour, 1));

        await SaveTheShelfAsync(inventoryId, [Row(flour, minimum: 5)], splitEvenlyAcross: [flour.Id]);

        var asked = new[] { bread, pizza, pasta }.Select(list => list.Items.Single().RequiredQuantity ?? 0).ToList();
        Assert.Equal(5, asked.Sum());
        Assert.Equal(5, flour.Usage);
    }

    /// <summary>
    /// Saving a shelf nobody touched the minimum of leaves every list alone. Without this, opening an
    /// inventory, correcting a count and pressing Save would quietly rewrite amounts across the lists.
    /// </summary>
    [Fact]
    public async Task A_save_that_changes_no_minimum_leaves_the_lists_alone()
    {
        var inventoryId = _context.AddInventory(UserId);
        var flour = AShelfItem(inventoryId, minimum: 2);
        var bread = await AListAsync("Bread", AnEntryFor(flour, 7));

        // The same minimum back, with something else on the row changed.
        await SaveTheShelfAsync(
            inventoryId,
            [new InventoryItemInput(
                flour.Id, flour.Name, "Baking", flour.Categories, Quantity: 3, MinimumQuantity: 2, flour.Unit,
                flour.ExpiryDate, flour.ExpiryNotificationChannel)]);

        Assert.Equal(7, bread.Items.Single().RequiredQuantity);
    }

    /// <summary>
    /// Orbit's own "Restock supplies" list is not a second list asking. Its errand points at the shelf
    /// item exactly as a real entry does, so before ManagedRestockLists existed it counted as one: every
    /// row that had ever run low looked shared, which blocked the write-back above, and its errand added
    /// one to what the shelf thought it was wanted for. Found writing these tests, 2026-09-18.
    /// </summary>
    [Fact]
    public async Task Orbits_own_restock_list_is_not_a_list_asking()
    {
        var inventoryId = _context.AddInventory(UserId);
        // Below its minimum from the start, so saving raises a restock errand pointing straight at it.
        var flour = AShelfItem(inventoryId, minimum: 2);
        var bread = await AListAsync("Bread", AnEntryFor(flour, 2));

        await SaveTheShelfAsync(inventoryId, [Row(flour, minimum: 6)]);

        var restockList = Assert.Single(
            await _context.TaskRepository.GetAllAsync(UserId, null, CancellationToken.None),
            taskList => taskList.Id != bread.Id);
        Assert.Contains(restockList.Items, item => item.LinkedInventoryItemId == flour.Id);

        var claims = await _context.ShelfDemand.WhoAsksForAsync(
            UserId, new HashSet<Guid> { flour.Id }, CancellationToken.None);
        Assert.Equal("Bread", Assert.Single(claims).TaskListName);
        // And the count is what the one real list asks for, not that plus Orbit's own errand.
        Assert.Equal(6, flour.Usage);
    }

    /// <summary>
    /// Clearing a minimum is not an amount to ask a list for - the shelf simply goes back to being kept
    /// at whatever the lists want. See UpdateInventoryCommandHandler.
    /// </summary>
    [Fact]
    public async Task Clearing_a_minimum_leaves_the_lists_alone()
    {
        var inventoryId = _context.AddInventory(UserId);
        var flour = AShelfItem(inventoryId, minimum: 2);
        var bread = await AListAsync("Bread", AnEntryFor(flour, 7));

        await SaveTheShelfAsync(inventoryId, [Row(flour, minimum: null)]);

        Assert.Equal(7, bread.Items.Single().RequiredQuantity);
    }
}

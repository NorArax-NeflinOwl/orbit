using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.GetInventoryReferences;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// What a restock errand carries besides its own words: the shelf item it is about, and any other list
/// asking for the same thing. Both exist so the reader can go and look - which is why the errand is a
/// kind with a link rather than a sentence with a product name parsed back out of it.
/// </summary>
public sealed class InventoryReferenceTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    [Fact]
    public async Task An_errand_raised_for_a_shelf_item_is_an_inventory_entry_pointing_at_it()
    {
        var (_, taskListId, item) = await ALowItemAsync();

        var taskList = await TaskListAsync(taskListId);
        var errand = taskList.Items.Single(entry => RestockTaskNaming.IsRestockEntry(entry.Description));

        // The link, not the description, is what everything else acts on: renaming the product used to
        // break the connection, because the connection was the product's name.
        Assert.Equal(TaskItemKind.Inventory, errand.Kind);
        Assert.Equal(item.Id, errand.LinkedInventoryItemId);
    }

    [Fact]
    public async Task The_standing_reminder_is_not_an_inventory_entry()
    {
        var (_, taskListId, _) = await ALowItemAsync();

        var reminder = (await TaskListAsync(taskListId)).Items
            .Single(entry => entry.Description == RestockTaskNaming.UpdateStockReminderDescription);

        // It is a claim about the whole shelf rather than about a product, and has nothing to point at.
        Assert.Equal(TaskItemKind.Checklist, reminder.Kind);
        Assert.Null(reminder.LinkedInventoryItemId);
    }

    [Fact]
    public async Task An_errand_says_which_shelf_it_is_about()
    {
        var (warehouseId, taskListId, item) = await ALowItemAsync();

        var reference = Assert.Single(await ReferencesAsync(taskListId));

        Assert.Equal(item.Id, reference.InventoryItemId);
        Assert.Equal("Flour", reference.InventoryItemName);
        Assert.Equal(warehouseId, reference.WarehouseId);
        Assert.Equal("Kitchen", reference.WarehouseName);
    }

    [Fact]
    public async Task A_product_asked_for_twice_says_where_else_it_is_being_asked_for()
    {
        var (_, taskListId, item) = await ALowItemAsync();

        // A second list, put together by hand, that happens to want the same thing. This is the case the
        // reference is for: somebody looking at one of two lists asking for one product.
        var byHand = TaskList.Create(
            _userId, "Saturday shopping",
            [TaskItem.Create("Restock: Flour", null, false, subject: new TaskItemSubject(TaskItemKind.Inventory, linkedInventoryItemId: item.Id))]);
        await _context.TaskRepository.AddAsync(byHand, CancellationToken.None);

        var reference = Assert.Single(await ReferencesAsync(taskListId));

        var elsewhere = Assert.Single(reference.AlsoAskedForBy);
        Assert.Equal(byHand.Id, elsewhere.TaskListId);
        Assert.Equal("Saturday shopping", elsewhere.TaskListTitle);
        Assert.Equal(byHand.Items[0].Id, elsewhere.TaskItemId);
    }

    [Fact]
    public async Task A_list_does_not_report_itself_as_somewhere_else()
    {
        var (_, taskListId, _) = await ALowItemAsync();

        var reference = Assert.Single(await ReferencesAsync(taskListId));

        Assert.Empty(reference.AlsoAskedForBy);
    }

    [Fact]
    public async Task An_errand_whose_product_has_been_deleted_has_nothing_to_point_at()
    {
        var (warehouseId, taskListId, item) = await ALowItemAsync();
        await _context.InventoryRepository.DeleteAsync(warehouseId, item.Id, CancellationToken.None);

        // The entry still reads as what it says. Offering a link to a shelf item that is gone would be
        // offering a page that cannot be opened.
        Assert.Empty(await ReferencesAsync(taskListId));
    }

    [Fact]
    public async Task An_ordinary_list_has_no_references_at_all()
    {
        var taskList = TaskList.Create(_userId, "Errands", [TaskItem.Create("Buy milk", null, false)]);
        await _context.TaskRepository.AddAsync(taskList, CancellationToken.None);

        Assert.Empty(await ReferencesAsync(taskList.Id));
    }

    [Fact]
    public async Task Somebody_else_is_told_nothing_about_this_list()
    {
        var (_, taskListId, _) = await ALowItemAsync();

        var references = await new GetInventoryReferencesQueryHandler(
                new TaskListAccessResolver(_context.TaskRepository, new InMemoryTaskListShareRepository(), _context.UserRepository),
                _context.TaskRepository, _context.WarehouseRepository, _context.InventoryRepository)
            .HandleAsync(new GetInventoryReferencesQuery(Guid.NewGuid(), taskListId), CancellationToken.None);

        Assert.Empty(references);
    }

    private async Task<(Guid WarehouseId, Guid TaskListId, InventoryItem Item)> ALowItemAsync()
    {
        var warehouseId = _context.AddWarehouse(_userId);
        var item = InventoryItem.Create(
            warehouseId, "Flour", "Food", "Dry", 0, 5, InventoryUnit.Piece, null, NotificationChannel.None);
        await _context.InventoryRepository.AddAsync(item, CancellationToken.None);
        var raised = await _context.TaskListCoordinator.EnsureRestockTaskAsync(item, CancellationToken.None);
        await _context.InventoryRepository.UpdateAsync(raised, CancellationToken.None);
        return (warehouseId, raised.PendingRestockTaskListId!.Value, raised);
    }

    private async Task<TaskList> TaskListAsync(Guid taskListId)
        => (await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None))!;

    private Task<IReadOnlyList<InventoryReference>> ReferencesAsync(Guid taskListId)
        => new GetInventoryReferencesQueryHandler(
                new TaskListAccessResolver(_context.TaskRepository, new InMemoryTaskListShareRepository(), _context.UserRepository),
                _context.TaskRepository, _context.WarehouseRepository, _context.InventoryRepository)
            .HandleAsync(new GetInventoryReferencesQuery(_userId, taskListId), CancellationToken.None);
}

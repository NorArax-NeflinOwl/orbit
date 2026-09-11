using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventories;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.LinkTaskListToInventory;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Several lists may be measured against one shelf. That used to be refused - two lists asking the same
/// shelf "is there enough" each got the whole shelf for an answer - and what answers it now is the
/// counting: the shelf is split between everything asking for it, which is held in
/// StockCheckSharedInventoryTests. What this file holds is that the linking itself is allowed.
/// </summary>
public sealed class ListsSharingAnInventoryTests
{
    [Fact]
    public async Task A_list_can_be_measured_against_an_inventory_nobody_has_taken()
    {
        var context = new LinkingContext();
        var inventory = await context.AInventoryAsync();
        var list = await context.AListAsync();

        Assert.True(await context.LinkAsync(list.Id, inventory.Id));
        Assert.Equal(inventory.Id, (await context.ReadAsync(list.Id))!.LinkedInventoryId);
    }

    /// <summary>
    /// A second list may measure the same shelf, and the first keeps it: one store serves several jobs,
    /// which is what a pantry is. What stops them double-counting is the stock check, not this.
    /// </summary>
    [Fact]
    public async Task A_inventory_another_list_already_measures_may_be_shared()
    {
        var context = new LinkingContext();
        var inventory = await context.AInventoryAsync();
        var first = await context.AListAsync();
        var second = await context.AListAsync();
        await context.LinkAsync(first.Id, inventory.Id);

        Assert.True(await context.LinkAsync(second.Id, inventory.Id));
        Assert.Equal(inventory.Id, (await context.ReadAsync(second.Id))!.LinkedInventoryId);
        Assert.Equal(inventory.Id, (await context.ReadAsync(first.Id))!.LinkedInventoryId);
    }

    /// <summary>An inventory this account cannot read is still refused - that gate has not moved.</summary>
    [Fact]
    public async Task A_inventory_that_is_not_there_is_refused()
    {
        var context = new LinkingContext();
        var list = await context.AListAsync();

        Assert.False(await context.LinkAsync(list.Id, Guid.NewGuid()));
        Assert.Null((await context.ReadAsync(list.Id))!.LinkedInventoryId);
    }

    /// <summary>Pointing the same list at it again is the state the caller is asking for, not a clash.</summary>
    [Fact]
    public async Task The_list_that_already_measures_it_may_say_so_again()
    {
        var context = new LinkingContext();
        var inventory = await context.AInventoryAsync();
        var list = await context.AListAsync();
        await context.LinkAsync(list.Id, inventory.Id);

        Assert.True(await context.LinkAsync(list.Id, inventory.Id));
    }

    /// <summary>A list can stop being measured against anything, which is what unlinking is for.</summary>
    [Fact]
    public async Task A_list_can_let_its_inventory_go()
    {
        var context = new LinkingContext();
        var inventory = await context.AInventoryAsync();
        var list = await context.AListAsync();
        await context.LinkAsync(list.Id, inventory.Id);

        Assert.True(await context.LinkAsync(list.Id, inventoryId: null));

        Assert.Null((await context.ReadAsync(list.Id))!.LinkedInventoryId);
    }

    [Fact]
    public async Task Two_lists_may_measure_two_different_inventories()
    {
        var context = new LinkingContext();
        var pantry = await context.AInventoryAsync();
        var shed = await context.AInventoryAsync();
        var first = await context.AListAsync();
        var second = await context.AListAsync();

        Assert.True(await context.LinkAsync(first.Id, pantry.Id));
        Assert.True(await context.LinkAsync(second.Id, shed.Id));
    }

    /// <summary>
    /// Linking puts the list's product entries on the shelf there and then, and points each at its row -
    /// before, a list linked and left alone had its products on no shelf until it was next saved.
    /// </summary>
    [Fact]
    public async Task Linking_puts_the_lists_products_on_the_shelf()
    {
        var context = new LinkingContext();
        var inventory = await context.AInventoryAsync();
        var list = await context.AListAsync(
            TaskItem.Create(
                "Mąka", dueDateUtc: null, isCompleted: false, subject: new TaskItemSubject(TaskItemKind.Inventory),
                product: TaskItemProduct.Default with { MinimumQuantity = 2 }),
            TaskItem.Create("Jajka", dueDateUtc: null, isCompleted: false));

        Assert.True(await context.LinkAsync(list.Id, inventory.Id));

        var row = Assert.Single(await context.ShelfAsync(inventory.Id));
        Assert.Equal("Mąka", row.Name);
        Assert.Equal(2, row.MinimumQuantity);
        var flour = Assert.Single((await context.ReadAsync(list.Id))!.Items, item => item.Description == "Mąka");
        Assert.Equal(row.Id, flour.LinkedInventoryItemId);
    }

    /// <summary>And letting the shelf go puts nothing anywhere: there is no shelf to put it on.</summary>
    [Fact]
    public async Task Unlinking_puts_nothing_on_any_shelf()
    {
        var context = new LinkingContext();
        var inventory = await context.AInventoryAsync();
        var list = await context.AListAsync(
            TaskItem.Create(
                "Mąka", dueDateUtc: null, isCompleted: false, subject: new TaskItemSubject(TaskItemKind.Inventory),
                product: TaskItemProduct.Default));

        Assert.True(await context.LinkAsync(list.Id, inventoryId: null));

        Assert.Empty(await context.ShelfAsync(inventory.Id));
    }

    /// <summary>The same collaborators DI hands the handler - see InventoryTestContext.</summary>
    private sealed class LinkingContext
    {
        private readonly InventoryTestContext _inventories = new();

        private Guid UserId { get; } = Guid.NewGuid();

        public async Task<Inventory> AInventoryAsync()
        {
            var inventory = Inventory.Create(UserId, "Pantry");
            await _inventories.InventoryRepository.AddAsync(inventory, CancellationToken.None);
            return inventory;
        }

        public async Task<TaskList> AListAsync(params TaskItem[] items)
        {
            var taskList = TaskList.Create(UserId, "Errands", items);
            await _inventories.TaskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public Task<bool> LinkAsync(Guid taskListId, Guid? inventoryId)
            => new LinkTaskListToInventoryCommandHandler(
                    _inventories.TaskRepository, _inventories.InventoryRepository, _inventories.ProductEntryPlacement)
                .HandleAsync(new LinkTaskListToInventoryCommand(UserId, taskListId, inventoryId), CancellationToken.None);

        public Task<TaskList?> ReadAsync(Guid taskListId)
            => _inventories.TaskRepository.GetByIdAsync(UserId, taskListId, CancellationToken.None);

        public Task<IReadOnlyList<InventoryItem>> ShelfAsync(Guid inventoryId)
            => _inventories.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None);
    }
}

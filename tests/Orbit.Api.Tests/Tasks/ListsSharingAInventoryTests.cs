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
public sealed class ListsSharingAInventoryTests
{
    [Fact]
    public async Task A_list_can_be_measured_against_a_inventory_nobody_has_taken()
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

    private sealed class LinkingContext
    {
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly InMemoryInventoryRepository _inventoryRepository = new();

        private Guid UserId { get; } = Guid.NewGuid();

        public async Task<Inventory> AInventoryAsync()
        {
            var inventory = Inventory.Create(UserId, "Pantry");
            await _inventoryRepository.AddAsync(inventory, CancellationToken.None);
            return inventory;
        }

        public async Task<TaskList> AListAsync()
        {
            var taskList = TaskList.Create(UserId, "Errands", []);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public Task<bool> LinkAsync(Guid taskListId, Guid? inventoryId)
            => new LinkTaskListToInventoryCommandHandler(_taskRepository, _inventoryRepository)
                .HandleAsync(new LinkTaskListToInventoryCommand(UserId, taskListId, inventoryId), CancellationToken.None);

        public Task<TaskList?> ReadAsync(Guid taskListId)
            => _taskRepository.GetByIdAsync(UserId, taskListId, CancellationToken.None);
    }
}

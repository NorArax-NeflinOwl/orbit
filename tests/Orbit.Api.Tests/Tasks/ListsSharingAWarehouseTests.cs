using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.LinkTaskListToWarehouse;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Several lists may be measured against one shelf. That used to be refused - two lists asking the same
/// shelf "is there enough" each got the whole shelf for an answer - and what answers it now is the
/// counting: the shelf is split between everything asking for it, which is held in
/// StockCheckSharedWarehouseTests. What this file holds is that the linking itself is allowed.
/// </summary>
public sealed class ListsSharingAWarehouseTests
{
    [Fact]
    public async Task A_list_can_be_measured_against_a_warehouse_nobody_has_taken()
    {
        var context = new LinkingContext();
        var warehouse = await context.AWarehouseAsync();
        var list = await context.AListAsync();

        Assert.True(await context.LinkAsync(list.Id, warehouse.Id));
        Assert.Equal(warehouse.Id, (await context.ReadAsync(list.Id))!.LinkedWarehouseId);
    }

    /// <summary>
    /// A second list may measure the same shelf, and the first keeps it: one store serves several jobs,
    /// which is what a pantry is. What stops them double-counting is the stock check, not this.
    /// </summary>
    [Fact]
    public async Task A_warehouse_another_list_already_measures_may_be_shared()
    {
        var context = new LinkingContext();
        var warehouse = await context.AWarehouseAsync();
        var first = await context.AListAsync();
        var second = await context.AListAsync();
        await context.LinkAsync(first.Id, warehouse.Id);

        Assert.True(await context.LinkAsync(second.Id, warehouse.Id));
        Assert.Equal(warehouse.Id, (await context.ReadAsync(second.Id))!.LinkedWarehouseId);
        Assert.Equal(warehouse.Id, (await context.ReadAsync(first.Id))!.LinkedWarehouseId);
    }

    /// <summary>A warehouse this account cannot read is still refused - that gate has not moved.</summary>
    [Fact]
    public async Task A_warehouse_that_is_not_there_is_refused()
    {
        var context = new LinkingContext();
        var list = await context.AListAsync();

        Assert.False(await context.LinkAsync(list.Id, Guid.NewGuid()));
        Assert.Null((await context.ReadAsync(list.Id))!.LinkedWarehouseId);
    }

    /// <summary>Pointing the same list at it again is the state the caller is asking for, not a clash.</summary>
    [Fact]
    public async Task The_list_that_already_measures_it_may_say_so_again()
    {
        var context = new LinkingContext();
        var warehouse = await context.AWarehouseAsync();
        var list = await context.AListAsync();
        await context.LinkAsync(list.Id, warehouse.Id);

        Assert.True(await context.LinkAsync(list.Id, warehouse.Id));
    }

    /// <summary>A list can stop being measured against anything, which is what unlinking is for.</summary>
    [Fact]
    public async Task A_list_can_let_its_warehouse_go()
    {
        var context = new LinkingContext();
        var warehouse = await context.AWarehouseAsync();
        var list = await context.AListAsync();
        await context.LinkAsync(list.Id, warehouse.Id);

        Assert.True(await context.LinkAsync(list.Id, warehouseId: null));

        Assert.Null((await context.ReadAsync(list.Id))!.LinkedWarehouseId);
    }

    [Fact]
    public async Task Two_lists_may_measure_two_different_warehouses()
    {
        var context = new LinkingContext();
        var pantry = await context.AWarehouseAsync();
        var shed = await context.AWarehouseAsync();
        var first = await context.AListAsync();
        var second = await context.AListAsync();

        Assert.True(await context.LinkAsync(first.Id, pantry.Id));
        Assert.True(await context.LinkAsync(second.Id, shed.Id));
    }

    private sealed class LinkingContext
    {
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly InMemoryWarehouseRepository _warehouseRepository = new();

        private Guid UserId { get; } = Guid.NewGuid();

        public async Task<Warehouse> AWarehouseAsync()
        {
            var warehouse = Warehouse.Create(UserId, "Pantry");
            await _warehouseRepository.AddAsync(warehouse, CancellationToken.None);
            return warehouse;
        }

        public async Task<TaskList> AListAsync()
        {
            var taskList = TaskList.Create(UserId, "Errands", []);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public Task<bool> LinkAsync(Guid taskListId, Guid? warehouseId)
            => new LinkTaskListToWarehouseCommandHandler(_taskRepository, _warehouseRepository)
                .HandleAsync(new LinkTaskListToWarehouseCommand(UserId, taskListId, warehouseId), CancellationToken.None);

        public Task<TaskList?> ReadAsync(Guid taskListId)
            => _taskRepository.GetByIdAsync(UserId, taskListId, CancellationToken.None);
    }
}

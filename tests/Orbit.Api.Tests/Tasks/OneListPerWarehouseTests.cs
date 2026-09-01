using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.LinkTaskListToWarehouse;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// A shelf is measured against one list. Two would give it two answers to "is there enough", and each
/// list's stock check would report a shortfall the other had already accounted for.
/// </summary>
public sealed class OneListPerWarehouseTests
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

    [Fact]
    public async Task A_warehouse_another_list_already_measures_is_refused()
    {
        var context = new LinkingContext();
        var warehouse = await context.AWarehouseAsync();
        var first = await context.AListAsync();
        var second = await context.AListAsync();
        await context.LinkAsync(first.Id, warehouse.Id);

        Assert.False(await context.LinkAsync(second.Id, warehouse.Id));
        // Refused rather than taken from the list that had it.
        Assert.Null((await context.ReadAsync(second.Id))!.LinkedWarehouseId);
        Assert.Equal(warehouse.Id, (await context.ReadAsync(first.Id))!.LinkedWarehouseId);
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

    [Fact]
    public async Task Letting_a_warehouse_go_frees_it_for_another_list()
    {
        var context = new LinkingContext();
        var warehouse = await context.AWarehouseAsync();
        var first = await context.AListAsync();
        var second = await context.AListAsync();
        await context.LinkAsync(first.Id, warehouse.Id);

        await context.LinkAsync(first.Id, warehouseId: null);

        Assert.True(await context.LinkAsync(second.Id, warehouse.Id));
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

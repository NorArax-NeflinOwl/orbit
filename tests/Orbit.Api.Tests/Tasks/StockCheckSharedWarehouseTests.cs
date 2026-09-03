using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.GetTaskListStockCheck;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// One shelf, several lists. A warehouse used to belong to a single list precisely so that "is there
/// enough" had one answer; now that several may share it, the answer is worked out from everything
/// asking at once and each list is told its share. Told the whole shelf instead, two lists would each
/// start work on the same last bag of flour.
/// </summary>
public sealed class StockCheckSharedWarehouseTests
{
    [Fact]
    public async Task A_shelf_nobody_else_asks_for_is_entirely_this_lists()
    {
        var context = new SharedShelfContext();
        var list = await context.AListAsync("Baking", "Flour", "Flour");
        await context.PutOnTheShelfAsync("Flour", quantity: 2);

        var check = await context.CheckAsync(list);

        var flour = Assert.Single(check!.Requirements);
        Assert.Equal(2, flour.Required);
        Assert.Equal(2, flour.Available);
        Assert.True(check.IsAchievable);
    }

    /// <summary>
    /// Two lists wanting two each out of a shelf holding one: the bag is split down the middle, and both
    /// are short by one and a half. Neither is told the whole bag is theirs, which is the answer that
    /// sends two people to the same shelf.
    /// </summary>
    [Fact]
    public async Task A_shelf_two_lists_ask_for_is_split_between_them()
    {
        var context = new SharedShelfContext();
        var baking = await context.AListAsync("Baking", "Flour", "Flour");
        var bread = await context.AListAsync("Bread", "Flour", "Flour");
        await context.PutOnTheShelfAsync("Flour", quantity: 1);

        var forBaking = Assert.Single((await context.CheckAsync(baking))!.Requirements);
        var forBread = Assert.Single((await context.CheckAsync(bread))!.Requirements);

        Assert.Equal(0.5m, forBaking.Available);
        Assert.Equal(1.5m, forBaking.Missing);
        Assert.Equal(0.5m, forBread.Available);
        Assert.Equal(1.5m, forBread.Missing);
    }

    /// <summary>The share follows what each asks for, so the list asking for more gets more of it.</summary>
    [Fact]
    public async Task The_shelf_is_split_in_proportion_to_what_each_list_asks_for()
    {
        var context = new SharedShelfContext();
        var many = await context.AListAsync("Baking", "Flour", "Flour", "Flour");
        var one = await context.AListAsync("Bread", "Flour");
        await context.PutOnTheShelfAsync("Flour", quantity: 4);

        Assert.Equal(3, Assert.Single((await context.CheckAsync(many))!.Requirements).Available);
        Assert.Equal(1, Assert.Single((await context.CheckAsync(one))!.Requirements).Available);
    }

    /// <summary>A list measured against a different warehouse is not asking for this shelf at all.</summary>
    [Fact]
    public async Task A_list_measured_against_another_warehouse_takes_no_share()
    {
        var context = new SharedShelfContext();
        var baking = await context.AListAsync("Baking", "Flour", "Flour");
        await context.AListAsync("Shed", inAnotherWarehouse: true, "Flour", "Flour");
        await context.PutOnTheShelfAsync("Flour", quantity: 2);

        var flour = Assert.Single((await context.CheckAsync(baking))!.Requirements);
        Assert.Equal(2, flour.Available);
        Assert.Equal(0, flour.Missing);
    }

    private sealed class SharedShelfContext
    {
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly InMemoryInventoryRepository _inventoryRepository = new();
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _warehouseId = Guid.NewGuid();
        private readonly Guid _otherWarehouseId = Guid.NewGuid();

        public async Task<TaskList> AListAsync(string title, params string[] entries)
            => await AListAsync(title, inAnotherWarehouse: false, entries);

        public Task<TaskList> AListAsync(string title, bool inAnotherWarehouse, params string[] entries)
            => AddAsync(title, inAnotherWarehouse, entries);

        private async Task<TaskList> AddAsync(string title, bool inAnotherWarehouse, string[] entries)
        {
            var taskList = TaskList.Create(
                _userId, title,
                [.. entries.Select(entry => TaskItem.Create(entry, dueDateUtc: null, isCompleted: false))]);
            taskList.LinkToWarehouse(inAnotherWarehouse ? _otherWarehouseId : _warehouseId);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public Task PutOnTheShelfAsync(string name, decimal quantity)
            => _inventoryRepository.AddAsync(
                InventoryItem.Create(
                    _warehouseId, name, productType: "", category: "", quantity, minimumQuantity: null,
                    InventoryUnit.Piece, expiryDate: null, NotificationChannel.None),
                CancellationToken.None);

        public Task<Orbit.Core.Tasks.StockCheck.TaskListStockCheck?> CheckAsync(TaskList taskList)
            => new GetTaskListStockCheckQueryHandler(_taskRepository, _inventoryRepository)
                .HandleAsync(new GetTaskListStockCheckQuery(_userId, taskList.Id), CancellationToken.None);
    }
}

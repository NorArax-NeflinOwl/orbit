using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.GetTaskListStockCheck;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// One shelf, several lists. An inventory used to belong to a single list precisely so that "is there
/// enough" had one answer; now that several may share it, the answer is worked out from everything
/// asking at once and each list is told its share. Told the whole shelf instead, two lists would each
/// start work on the same last bag of flour.
/// </summary>
public sealed class StockCheckSharedInventoryTests
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

    /// <summary>A list measured against a different inventory is not asking for this shelf at all.</summary>
    [Fact]
    public async Task A_list_measured_against_another_inventory_takes_no_share()
    {
        var context = new SharedShelfContext();
        var baking = await context.AListAsync("Baking", "Flour", "Flour");
        await context.AListAsync("Shed", inAnotherInventory: true, "Flour", "Flour");
        await context.PutOnTheShelfAsync("Flour", quantity: 2);

        var flour = Assert.Single((await context.CheckAsync(baking))!.Requirements);
        Assert.Equal(2, flour.Available);
        Assert.Equal(0, flour.Missing);
    }

    /// <summary>
    /// Two lists each standing for the same shelf row: the row's minimum is what their entries handed over
    /// together, so it is asked for once, and a shelf holding exactly that is enough for both. Counted once
    /// per list, each was told the other wanted the whole minimum too and both looked short.
    /// </summary>
    [Fact]
    public async Task Two_lists_standing_for_one_row_ask_for_its_minimum_once()
    {
        var context = new SharedShelfContext();
        var flour = await context.PutOnTheShelfAsync("Flour", quantity: 4, minimum: 4);
        var baking = await context.AListStandingForAsync("Baking", "Flour", flour.Id);
        var bread = await context.AListStandingForAsync("Bread", "Flour", flour.Id);

        var forBaking = Assert.Single((await context.CheckAsync(baking))!.Requirements);
        var forBread = Assert.Single((await context.CheckAsync(bread))!.Requirements);

        Assert.Equal(4, forBaking.Required);
        Assert.Equal(4, forBaking.Available);
        Assert.Equal(0, forBaking.Missing);
        Assert.Equal(4, forBread.Available);
        Assert.Equal(0, forBread.Missing);
    }

    private sealed class SharedShelfContext
    {
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly InMemoryInventoryItemRepository _inventoryItemRepository = new();
        private readonly Guid _userId = Guid.NewGuid();
        private readonly Guid _inventoryId = Guid.NewGuid();
        private readonly Guid _otherInventoryId = Guid.NewGuid();

        public async Task<TaskList> AListAsync(string title, params string[] entries)
            => await AListAsync(title, inAnotherInventory: false, entries);

        public Task<TaskList> AListAsync(string title, bool inAnotherInventory, params string[] entries)
            => AddAsync(title, inAnotherInventory, entries);

        private async Task<TaskList> AddAsync(string title, bool inAnotherInventory, string[] entries)
        {
            var taskList = TaskList.Create(
                _userId, title,
                [.. entries.Select(entry => TaskItem.Create(entry, dueDateUtc: null, isCompleted: false))]);
            taskList.LinkToInventory(inAnotherInventory ? _otherInventoryId : _inventoryId);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public Task PutOnTheShelfAsync(string name, decimal quantity)
            => _inventoryItemRepository.AddAsync(
                InventoryItem.Create(
                    _inventoryId, name, productType: "", categories: [], quantity, minimumQuantity: null,
                    InventoryUnit.Piece, expiryDate: null, NotificationChannel.None),
                CancellationToken.None);

        /// <summary>A row with a minimum of its own - what a shelf generated from a list carries.</summary>
        public async Task<InventoryItem> PutOnTheShelfAsync(string name, decimal quantity, decimal minimum)
        {
            var row = InventoryItem.Create(
                _inventoryId, name, productType: "", categories: [], quantity, minimum,
                InventoryUnit.Piece, expiryDate: null, NotificationChannel.None);
            await _inventoryItemRepository.AddAsync(row, CancellationToken.None);
            return row;
        }

        /// <summary>A list with one product entry standing for a row already on the shelf.</summary>
        public async Task<TaskList> AListStandingForAsync(string title, string entry, Guid shelfItemId)
        {
            var standing = TaskItem.Create(
                entry, dueDateUtc: null, isCompleted: false, subject: new TaskItemSubject(TaskItemKind.Inventory));
            standing.PointAtShelfItem(shelfItemId);
            var taskList = TaskList.Create(_userId, title, [standing]);
            taskList.LinkToInventory(_inventoryId);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public Task<Orbit.Core.Tasks.StockCheck.TaskListStockCheck?> CheckAsync(TaskList taskList)
            => new GetTaskListStockCheckQueryHandler(_taskRepository, _inventoryItemRepository)
                .HandleAsync(new GetTaskListStockCheckQuery(_userId, taskList.Id), CancellationToken.None);
    }
}

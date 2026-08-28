using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.CompleteWorkCoveredByStock;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Crossing off the work a warehouse already covers - the other half of pricing a list against one.
/// </summary>
public sealed class CompleteWorkCoveredByStockCommandHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();
    private readonly Guid _warehouseId;

    public CompleteWorkCoveredByStockCommandHandlerTests()
    {
        _warehouseId = _context.AddWarehouse(_userId);
    }

    private static TaskItem Work(string description, bool isCompleted = false, DateTimeOffset? dueDateUtc = null)
        => TaskItem.Create(description, dueDateUtc, isCompleted);

    private static TaskItem LinkTo(TaskList target)
        => TaskItem.Create(target.Title, dueDateUtc: null, isCompleted: false, linkedTaskListId: target.Id);

    private TaskList Store(string title, bool isGroup, params TaskItem[] items)
    {
        var taskList = TaskList.Create(_userId, title, items, isGroup);
        _context.TaskRepository.AddAsync(taskList, CancellationToken.None).GetAwaiter().GetResult();
        return taskList;
    }

    private void Stock(string name, decimal quantity)
        => _context.InventoryRepository.AddAsync(
            InventoryItem.Create(_warehouseId, name, "Part", "Shelf", quantity, null, null, NotificationChannel.None),
            CancellationToken.None).GetAwaiter().GetResult();

    private TaskList LinkedToTheWarehouse(TaskList taskList)
    {
        taskList.LinkToWarehouse(_warehouseId);
        _context.TaskRepository.UpdateAsync(taskList, CancellationToken.None).GetAwaiter().GetResult();
        return taskList;
    }

    private Task<int> RunAsync(Guid taskListId)
        => new CompleteWorkCoveredByStockCommandHandler(_context.TaskRepository, _context.InventoryRepository)
            .HandleAsync(new CompleteWorkCoveredByStockCommand(_userId, taskListId), CancellationToken.None);

    private IReadOnlyList<(string Description, bool IsCompleted)> ItemsIn(Guid taskListId)
        => [.. _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None)
            .GetAwaiter().GetResult()!.Items.Select(item => (item.Description, item.IsCompleted))];

    [Fact]
    public async Task What_the_shelf_holds_is_crossed_off()
    {
        var shopping = LinkedToTheWarehouse(Store("Zakupy", isGroup: false, Work("Mleko"), Work("Chleb")));
        Stock("Mleko", 1);

        var crossedOff = await RunAsync(shopping.Id);

        Assert.Equal(1, crossedOff);
        Assert.Equal([("Mleko", true), ("Chleb", false)], ItemsIn(shopping.Id));
    }

    [Fact]
    public async Task A_shelf_holding_three_of_five_crosses_off_three()
    {
        var shopping = LinkedToTheWarehouse(Store(
            "Zakupy", isGroup: false, Work("Makaron"), Work("Makaron"), Work("Makaron"), Work("Makaron"), Work("Makaron")));
        Stock("Makaron", 3);

        var crossedOff = await RunAsync(shopping.Id);

        Assert.Equal(3, crossedOff);
        Assert.Equal(3, ItemsIn(shopping.Id).Count(item => item.IsCompleted));
    }

    [Fact]
    public async Task Stock_already_spent_on_a_finished_entry_is_not_spent_again()
    {
        // Three on the shelf with one line already crossed off covers two more, not three - otherwise
        // the same three jars would finish four lines.
        var shopping = LinkedToTheWarehouse(Store(
            "Zakupy", isGroup: false, Work("Dżem", isCompleted: true), Work("Dżem"), Work("Dżem"), Work("Dżem")));
        Stock("Dżem", 3);

        var crossedOff = await RunAsync(shopping.Id);

        Assert.Equal(2, crossedOff);
        Assert.Equal(3, ItemsIn(shopping.Id).Count(item => item.IsCompleted));
    }

    [Fact]
    public async Task The_whole_tree_is_crossed_off_not_just_the_top_list()
    {
        var recipe = Store("Kolacja", isGroup: false, Work("Ser"));
        var shopping = LinkedToTheWarehouse(Store("Zakupy", isGroup: true, LinkTo(recipe), Work("Mleko")));
        Stock("Ser", 1);
        Stock("Mleko", 1);

        var crossedOff = await RunAsync(shopping.Id);

        Assert.Equal(2, crossedOff);
        Assert.Equal([("Ser", true)], ItemsIn(recipe.Id));
    }

    [Fact]
    public async Task Work_that_is_not_due_yet_is_left_alone()
    {
        // The check does not count it, so the shelf must not finish it either.
        var shopping = LinkedToTheWarehouse(Store(
            "Zakupy", isGroup: false, Work("Mąka", dueDateUtc: DateTimeOffset.UtcNow.AddDays(7))));
        Stock("Mąka", 5);

        Assert.Equal(0, await RunAsync(shopping.Id));
        Assert.Equal([("Mąka", false)], ItemsIn(shopping.Id));
    }

    [Fact]
    public async Task An_empty_shelf_crosses_off_nothing()
    {
        var shopping = LinkedToTheWarehouse(Store("Zakupy", isGroup: false, Work("Mleko")));

        Assert.Equal(0, await RunAsync(shopping.Id));
        Assert.Equal([("Mleko", false)], ItemsIn(shopping.Id));
    }

    [Fact]
    public async Task A_list_pointed_at_no_warehouse_has_nothing_to_answer_with()
    {
        var shopping = Store("Zakupy", isGroup: false, Work("Mleko"));
        Stock("Mleko", 5);

        Assert.Equal(0, await RunAsync(shopping.Id));
    }

    [Fact]
    public async Task Somebody_elses_list_is_left_alone()
    {
        var shopping = LinkedToTheWarehouse(Store("Zakupy", isGroup: false, Work("Mleko")));
        Stock("Mleko", 5);

        var handler = new CompleteWorkCoveredByStockCommandHandler(_context.TaskRepository, _context.InventoryRepository);
        var crossedOff = await handler.HandleAsync(
            new CompleteWorkCoveredByStockCommand(Guid.NewGuid(), shopping.Id), CancellationToken.None);

        Assert.Equal(0, crossedOff);
        Assert.Equal([("Mleko", false)], ItemsIn(shopping.Id));
    }
}

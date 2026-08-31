using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory.CreateWarehouse;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.GenerateWarehouseFromTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Generating the shelf a group list's work needs. The shape here is the one that showed the bug: a
/// shopping list whose own rows are all links, with the same ingredient named by three recipes under it.
/// </summary>
public sealed class GenerateWarehouseFromTaskListCommandHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    private static TaskItem Work(string description, DateTimeOffset? dueDateUtc = null, bool isCompleted = false)
        => TaskItem.Create(description, dueDateUtc, isCompleted);

    private static TaskItem LinkTo(TaskList target)
        => TaskItem.Create(target.Title, dueDateUtc: null, isCompleted: false, linkedTaskListId: target.Id);

    private TaskList Store(string title, bool isGroup, params TaskItem[] items)
    {
        var taskList = TaskList.Create(_userId, title, items, isGroup);
        _context.TaskRepository.AddAsync(taskList, CancellationToken.None).GetAwaiter().GetResult();
        return taskList;
    }

    private GenerateWarehouseFromTaskListCommandHandler AHandler()
        => new(
            new WarehouseCreatingDispatcher(new CreateWarehouseCommandHandler(_context.WarehouseRepository)),
            _context.TaskRepository, _context.InventoryRepository, _context.TaskListCoordinator);

    private IReadOnlyList<(string Name, decimal Quantity, decimal? Minimum)> ShelfIn(Guid warehouseId)
        => [.. _context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None)
            .GetAwaiter().GetResult()
            .Select(item => (item.Name, item.Quantity, item.MinimumQuantity))];

    /// <summary>The tree from the report: Zakupy links to three recipes, each of which calls for pasta.</summary>
    private TaskList AShoppingTree()
    {
        var withBroccoli = Store("Makaron w sosie z brokułami", isGroup: false, Work("Makaron świderki"), Work("Brokuł"));
        var withEgg = Store("Makaron z jajkiem", isGroup: false, Work("Makaron świderki"), Work("Jajka"));
        var withCurd = Store("Makaron z twarogiem", isGroup: false, Work("makaron świderki "), Work("Twaróg"));
        var recipes = Store("Przepisy", isGroup: true, LinkTo(withBroccoli), LinkTo(withEgg), LinkTo(withCurd));
        return Store("Zakupy", isGroup: true, LinkTo(recipes));
    }

    [Fact]
    public async Task Something_named_by_three_lists_needs_three_of_it()
    {
        var shopping = AShoppingTree();

        var warehouseId = await AHandler().HandleAsync(
            new GenerateWarehouseFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        // The whole point of generating a shelf: it comes out saying how many, not just what.
        var pasta = Assert.Single(ShelfIn(warehouseId!.Value), entry => entry.Name == "Makaron świderki");
        Assert.Equal(3, pasta.Minimum);
        Assert.Equal(0, pasta.Quantity);
    }

    [Fact]
    public async Task Everything_the_tree_names_gets_an_entry_of_its_own()
    {
        var shopping = AShoppingTree();

        var warehouseId = await AHandler().HandleAsync(
            new GenerateWarehouseFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        // One entry per distinct thing, "makaron świderki " and "Makaron świderki" being the same thing.
        Assert.Equal(
            [("Makaron świderki", 3m), ("Brokuł", 1m), ("Jajka", 1m), ("Twaróg", 1m)],
            ShelfIn(warehouseId!.Value).Select(entry => (entry.Name, entry.Minimum)));
    }

    [Fact]
    public async Task Nothing_is_on_the_shelf_until_something_has_been_done()
    {
        var shopping = AShoppingTree();

        var warehouseId = await AHandler().HandleAsync(
            new GenerateWarehouseFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        // A shelf that began full would report the job as doable before anybody had fetched anything.
        Assert.All(ShelfIn(warehouseId!.Value), entry => Assert.Equal(0, entry.Quantity));
    }

    [Fact]
    public async Task What_the_work_has_already_ticked_off_is_on_the_shelf()
    {
        // A crossed-out line is something somebody has, so the shelf starts with it rather than at zero.
        var oneBought = Store("Kolacja", isGroup: false, Work("Makaron świderki", isCompleted: true), Work("Ser"));
        var alsoNeeded = Store("Obiad", isGroup: false, Work("Makaron świderki"), Work("Makaron świderki"));
        var shopping = Store("Zakupy", isGroup: true, LinkTo(oneBought), LinkTo(alsoNeeded));

        var warehouseId = await AHandler().HandleAsync(
            new GenerateWarehouseFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        var pasta = Assert.Single(ShelfIn(warehouseId!.Value), entry => entry.Name == "Makaron świderki");
        Assert.Equal(3, pasta.Minimum);
        Assert.Equal(1, pasta.Quantity);
        Assert.Equal(0, Assert.Single(ShelfIn(warehouseId.Value), entry => entry.Name == "Ser").Quantity);
    }

    [Fact]
    public async Task Work_dated_ahead_is_still_something_the_job_will_need()
    {
        var later = Store("Later", isGroup: false, Work("Mąka", DateTimeOffset.UtcNow.AddDays(7)));
        var shopping = Store("Zakupy", isGroup: true, LinkTo(later), Work("Mąka"));

        var warehouseId = await AHandler().HandleAsync(
            new GenerateWarehouseFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        // The shelf holds what the whole job needs; leaving today's work out of it is the stock check's job.
        Assert.Equal(2, Assert.Single(ShelfIn(warehouseId!.Value)).Minimum);
    }

    [Fact]
    public async Task The_list_is_pointed_at_what_was_generated()
    {
        var shopping = AShoppingTree();
        // Aged first - see InMemoryTaskRepository.PretendItWasLastChanged. Comparing against the stamp
        // the tree was built with asks whether the clock ticked in between, which is not what this test
        // is about.
        var before = DateTimeOffset.UtcNow.AddMinutes(-1);
        _context.TaskRepository.PretendItWasLastChanged(shopping.Id, before);

        var warehouseId = await AHandler().HandleAsync(
            new GenerateWarehouseFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, shopping.Id, CancellationToken.None);
        Assert.Equal(warehouseId, stored!.LinkedWarehouseId);
        // And says so, or the change reaches nobody: the list now points somewhere it did not, and a
        // client reading the change feed would go on showing it measured against nothing at all.
        Assert.True(stored.UpdatedAtUtc > before);
    }

    [Fact]
    public async Task Somebody_elses_list_generates_nothing()
    {
        var shopping = AShoppingTree();

        var warehouseId = await AHandler().HandleAsync(
            new GenerateWarehouseFromTaskListCommand(Guid.NewGuid(), shopping.Id), CancellationToken.None);

        Assert.Null(warehouseId);
    }

    /// <summary>
    /// Routes the one command the handler sends to the real handler behind it, so what a generated
    /// warehouse is called and who owns it is exercised rather than stubbed.
    /// </summary>
    private sealed class WarehouseCreatingDispatcher : IDispatcher
    {
        private readonly CreateWarehouseCommandHandler _createWarehouse;

        public WarehouseCreatingDispatcher(CreateWarehouseCommandHandler createWarehouse)
        {
            _createWarehouse = createWarehouse;
        }

        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is CreateWarehouseCommand createWarehouse)
            {
                return (TResponse)(object)await _createWarehouse.HandleAsync(createWarehouse, cancellationToken);
            }

            throw new InvalidOperationException($"Nothing here handles {request.GetType().Name}.");
        }
    }
}

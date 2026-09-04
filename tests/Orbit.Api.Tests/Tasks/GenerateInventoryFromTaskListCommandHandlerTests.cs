using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.CreateInventory;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.GenerateInventoryFromTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Generating the shelf a group list's work needs. The shape here is the one that showed the bug: a
/// shopping list whose own rows are all links, with the same ingredient named by three recipes under it.
/// </summary>
public sealed class GenerateInventoryFromTaskListCommandHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    private static TaskItem Work(string description, DateTimeOffset? dueDateUtc = null, bool isCompleted = false)
        => TaskItem.Create(description, dueDateUtc, isCompleted);

    private static TaskItem LinkTo(TaskList target)
        => TaskItem.Create(target.Title, dueDateUtc: null, isCompleted: false, linkedTaskListIds: [target.Id]);

    private TaskList Store(string title, bool isGroup, params TaskItem[] items)
    {
        var taskList = TaskList.Create(_userId, title, items, isGroup);
        _context.TaskRepository.AddAsync(taskList, CancellationToken.None).GetAwaiter().GetResult();
        return taskList;
    }

    private GenerateInventoryFromTaskListCommandHandler AHandler()
        => new(
            new InventoryCreatingDispatcher(
                new CreateInventoryCommandHandler(_context.InventoryRepository, _context.ItemsSaver)),
            _context.TaskRepository, _context.InventoryItemRepository, _context.ManagedTaskListRepository,
            _context.RestockListRefresh);

    /// <summary>An entry that describes the thing it names - what the web's inventory fields write onto it.</summary>
    private static TaskItem Asking(string description, TaskItemProduct product)
        => TaskItem.Create(
            description, dueDateUtc: null, isCompleted: false,
            subject: new TaskItemSubject(TaskItemKind.Inventory), product: product);

    /// <summary>The shelf itself, for what the rows carry beyond a name and two numbers.</summary>
    private async Task<IReadOnlyList<InventoryItem>> ProductsIn(Guid inventoryId)
        => await _context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None);

    /// <summary>The "Restock supplies" list this storage keeps, or null when it keeps none.</summary>
    private async Task<TaskList?> RestockListOf(Guid inventoryId)
        => await _context.ManagedTaskListRepository.GetTaskListIdAsync(inventoryId, CancellationToken.None) is { } taskListId
            ? await _context.TaskRepository.GetByIdAsync(_userId, taskListId, CancellationToken.None)
            : null;

    private IReadOnlyList<(string Name, decimal Quantity, decimal? Minimum)> ShelfIn(Guid inventoryId)
        => [.. _context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None)
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

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        // The whole point of generating a shelf: it comes out saying how many, not just what.
        var pasta = Assert.Single(ShelfIn(inventoryId!.Value), entry => entry.Name == "Makaron świderki");
        Assert.Equal(3, pasta.Minimum);
        Assert.Equal(0, pasta.Quantity);
    }

    [Fact]
    public async Task Everything_the_tree_names_gets_an_entry_of_its_own()
    {
        var shopping = AShoppingTree();

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        // One entry per distinct thing, "makaron świderki " and "Makaron świderki" being the same thing.
        Assert.Equal(
            [("Makaron świderki", 3m), ("Brokuł", 1m), ("Jajka", 1m), ("Twaróg", 1m)],
            ShelfIn(inventoryId!.Value).Select(entry => (entry.Name, entry.Minimum)));
    }

    [Fact]
    public async Task Nothing_is_on_the_shelf_until_something_has_been_done()
    {
        var shopping = AShoppingTree();

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        // A shelf that began full would report the job as doable before anybody had fetched anything.
        Assert.All(ShelfIn(inventoryId!.Value), entry => Assert.Equal(0, entry.Quantity));
    }

    [Fact]
    public async Task What_the_work_has_already_ticked_off_is_on_the_shelf()
    {
        // A crossed-out line is something somebody has, so the shelf starts with it rather than at zero.
        var oneBought = Store("Kolacja", isGroup: false, Work("Makaron świderki", isCompleted: true), Work("Ser"));
        var alsoNeeded = Store("Obiad", isGroup: false, Work("Makaron świderki"), Work("Makaron świderki"));
        var shopping = Store("Zakupy", isGroup: true, LinkTo(oneBought), LinkTo(alsoNeeded));

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        var pasta = Assert.Single(ShelfIn(inventoryId!.Value), entry => entry.Name == "Makaron świderki");
        Assert.Equal(3, pasta.Minimum);
        Assert.Equal(1, pasta.Quantity);
        Assert.Equal(0, Assert.Single(ShelfIn(inventoryId.Value), entry => entry.Name == "Ser").Quantity);
    }

    [Fact]
    public async Task Work_dated_ahead_is_still_something_the_job_will_need()
    {
        var later = Store("Later", isGroup: false, Work("Mąka", DateTimeOffset.UtcNow.AddDays(7)));
        var shopping = Store("Zakupy", isGroup: true, LinkTo(later), Work("Mąka"));

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        // The shelf holds what the whole job needs; leaving today's work out of it is the stock check's job.
        Assert.Equal(2, Assert.Single(ShelfIn(inventoryId!.Value)).Minimum);
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

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, shopping.Id, CancellationToken.None);
        Assert.Equal(inventoryId, stored!.LinkedInventoryId);
        // And says so, or the change reaches nobody: the list now points somewhere it did not, and a
        // client reading the change feed would go on showing it measured against nothing at all.
        Assert.True(stored.UpdatedAtUtc > before);
    }

    /// <summary>
    /// An entry that describes the thing it names is taken at its word - see TaskItemProduct. The
    /// counting rule only ever answered "how many", and everything else about a generated row had to be
    /// typed again on the storage afterwards.
    /// </summary>
    [Fact]
    public async Task An_entry_that_describes_what_it_needs_is_put_on_the_shelf_as_described()
    {
        var shopping = Store("Zakupy", isGroup: false, Asking("Mąka", TaskItemProduct.Default with
        {
            ProductType = "Dry goods",
            Categories = ["Baking", "Dry goods"],
            Quantity = 2,
            MinimumQuantity = 5,
            Unit = InventoryUnit.Kilogram,
            IsCheckedRegularly = true
        }));

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        var flour = Assert.Single(await ProductsIn(inventoryId!.Value));
        Assert.Equal("Dry goods", flour.ProductType);
        Assert.Equal(["Baking", "Dry goods"], flour.Categories);
        Assert.Equal(2, flour.Quantity);
        Assert.Equal(5, flour.MinimumQuantity);
        Assert.Equal(InventoryUnit.Kilogram, flour.Unit);
        Assert.True(flour.IsCheckedRegularly);
    }

    /// <summary>
    /// A box nobody filled in is not an answer: the blanks fall back to what generating a shelf from a
    /// list has always done, which is what keeps "the same name twice asks for two" true for a list
    /// somebody wrote without opening a single entry.
    /// </summary>
    [Fact]
    public async Task What_an_entry_leaves_blank_is_still_counted_off_the_list()
    {
        var shopping = Store(
            "Zakupy", isGroup: false, Asking("Mąka", TaskItemProduct.Default), Asking("Mąka", TaskItemProduct.Default));

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        var flour = Assert.Single(await ProductsIn(inventoryId!.Value));
        Assert.Equal(2, flour.MinimumQuantity);
        Assert.Equal(0, flour.Quantity);
        Assert.Equal("Part", flour.ProductType);
        Assert.Equal(["From a task list"], flour.Categories);
    }

    /// <summary>
    /// And the entry then stands for the row it asked for. That link is what every other screen reads an
    /// errand through - see GetInventoryReferences - and it is also what makes the description on the
    /// entry redundant, so it is handed over rather than kept in two places.
    /// </summary>
    [Fact]
    public async Task An_entry_ends_up_pointing_at_the_row_it_asked_for()
    {
        var shopping = Store("Zakupy", isGroup: false, Asking("Mąka", TaskItemProduct.Default), Work("Jajka"));

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id), CancellationToken.None);

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, shopping.Id, CancellationToken.None);
        var flourOnTheShelf = Assert.Single(await ProductsIn(inventoryId!.Value), item => item.Name == "Mąka");
        var flourOnTheList = Assert.Single(stored!.Items, item => item.Description == "Mąka");
        Assert.Equal(flourOnTheShelf.Id, flourOnTheList.LinkedInventoryItemId);
        Assert.Null(flourOnTheList.Product);
        // An ordinary line is left as it was: it is work, not an errand about a product.
        Assert.Null(Assert.Single(stored.Items, item => item.Description == "Jajka").LinkedInventoryItemId);
    }

    [Fact]
    public async Task The_storage_is_called_what_the_form_asked_for()
    {
        var shopping = Store("Zakupy", isGroup: false, Work("Mąka"));

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id, "Spiżarnia"), CancellationToken.None);

        var inventory = await _context.InventoryRepository.GetByIdAsync(_userId, inventoryId!.Value, CancellationToken.None);
        Assert.Equal("Spiżarnia", inventory!.Name);
    }

    /// <summary>Which is the list's own title when nobody said - the answer the form offers.</summary>
    [Fact]
    public async Task A_storage_nobody_named_is_called_after_the_list()
    {
        var shopping = Store("Zakupy", isGroup: false, Work("Mąka"));

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id, "   "), CancellationToken.None);

        var inventory = await _context.InventoryRepository.GetByIdAsync(_userId, inventoryId!.Value, CancellationToken.None);
        Assert.Equal("Zakupy", inventory!.Name);
    }

    /// <summary>
    /// The restock list the storage keeps is built the way the form asked, not the default way and then
    /// corrected: the settings are written before the first row goes on the shelf.
    /// </summary>
    [Fact]
    public async Task The_restock_list_is_built_the_way_the_form_asked_for_it()
    {
        var shopping = Store("Zakupy", isGroup: false, Work("Mąka"));
        var settings = new RestockListSettings(
            OnlyLinkedWithDueDate: false, new TimeOnly(7, 30), IsEnabled: true, RemindDaily: true,
            ItemPriority.High, OnlyCheckedRegularly: false, NotificationChannel.Email);

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id, Name: null, settings), CancellationToken.None);

        Assert.Equal(
            settings,
            await _context.ManagedTaskListRepository.GetSettingsAsync(inventoryId!.Value, CancellationToken.None));
        var restockList = await RestockListOf(inventoryId.Value);
        Assert.Equal(ItemPriority.High, restockList!.Priority);
        var reminder = Assert.Single(
            restockList.Items, item => item.Description == InventoryTaskListCoordinator.UpdateStockReminderDescription);
        Assert.Equal(new TimeOnly(7, 30), reminder.DailyReminderTimeOfDay);
        Assert.Equal(NotificationChannel.Email, reminder.DailyReminderNotificationChannel);
        // The shelf is empty of everything the work needs, so the list asks for it straight away rather
        // than waiting for somebody to save the storage once.
        Assert.Contains(restockList.Items, item => item.LinkedInventoryItemId is not null);
    }

    /// <summary>Nothing is created for a storage whose restock list was switched off in the same form.</summary>
    [Fact]
    public async Task A_storage_generated_without_a_restock_list_has_none()
    {
        var shopping = Store("Zakupy", isGroup: false, Work("Mąka"));
        var settings = RestockListSettings.Default with { IsEnabled = false };

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id, Name: null, settings), CancellationToken.None);

        Assert.Null(await RestockListOf(inventoryId!.Value));
    }

    /// <summary>
    /// A list narrowed to the round asks only about what somebody marked to look at - see
    /// RestockListSettings.OnlyCheckedRegularly. The rest of the shelf is still there; it just raises
    /// nothing.
    /// </summary>
    [Fact]
    public async Task A_restock_list_set_to_the_round_asks_only_about_what_is_looked_at()
    {
        var shopping = Store(
            "Zakupy", isGroup: false,
            Asking("Mleko", TaskItemProduct.Default with { IsCheckedRegularly = true }),
            Asking("Mąka", TaskItemProduct.Default));
        var settings = RestockListSettings.Default with { OnlyCheckedRegularly = true };

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(_userId, shopping.Id, Name: null, settings), CancellationToken.None);

        var restockList = await RestockListOf(inventoryId!.Value);
        var errands = restockList!.Items.Where(item => item.LinkedInventoryItemId is not null).ToList();
        var milk = Assert.Single(await ProductsIn(inventoryId.Value), item => item.Name == "Mleko");
        Assert.Equal(milk.Id, Assert.Single(errands).LinkedInventoryItemId);
    }

    [Fact]
    public async Task Somebody_elses_list_generates_nothing()
    {
        var shopping = AShoppingTree();

        var inventoryId = await AHandler().HandleAsync(
            new GenerateInventoryFromTaskListCommand(Guid.NewGuid(), shopping.Id), CancellationToken.None);

        Assert.Null(inventoryId);
    }

    /// <summary>
    /// Routes the one command the handler sends to the real handler behind it, so what a generated
    /// inventory is called and who owns it is exercised rather than stubbed.
    /// </summary>
    private sealed class InventoryCreatingDispatcher : IDispatcher
    {
        private readonly CreateInventoryCommandHandler _createInventory;

        public InventoryCreatingDispatcher(CreateInventoryCommandHandler createInventory)
        {
            _createInventory = createInventory;
        }

        public async Task<TResponse> SendAsync<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
        {
            if (request is CreateInventoryCommand createInventory)
            {
                return (TResponse)(object)await _createInventory.HandleAsync(createInventory, cancellationToken);
            }

            throw new InvalidOperationException($"Nothing here handles {request.GetType().Name}.");
        }
    }
}

using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.UpdateWarehouse;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.UpdateTaskList;
using Xunit;

namespace Orbit.Api.Tests;

/// <summary>
/// What happens to a field a caller said nothing about.
///
/// Descriptions and the regular-check flag are new, and the clients learn about them at different
/// times: the browser deploys with the server, the phone whenever somebody installs it. A save replaces
/// what it touches wholesale, so a client that does not know a field exists returns the row without it -
/// and if that read as "clear it", every save from an older phone would wipe a description written on
/// the web. That is the exact shape of bug this codebase has already had three times.
///
/// So null means "not provided" and keeps what is stored; an empty string, or false, means the caller
/// really did say so. It costs one distinction to write down and removes the need for the two clients
/// to ship in lockstep.
/// </summary>
public sealed class UnchangedWhenNotProvidedTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    private static UpdateTaskListCommandHandler ATaskListHandler(InMemoryTaskRepository tasks)
        => new(
            new TaskListAccessResolver(tasks, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
            tasks,
            new TaskListLinkValidator(tasks),
            new RestockCompletion(
                new InMemoryInventoryManagedTaskListRepository(), new InMemoryInventoryRepository(),
                new InMemoryWarehouseRepository(), new InMemoryTaskRepository()));

    private static async Task<(InMemoryTaskRepository Tasks, Guid Id)> ADescribedListAsync()
    {
        var tasks = new InMemoryTaskRepository();
        var taskList = TaskList.Create(UserId, "Zakupy", [], description: "Na sobotę");
        await tasks.AddAsync(taskList, CancellationToken.None);
        return (tasks, taskList.Id);
    }

    [Fact]
    public async Task A_save_that_says_nothing_about_the_description_keeps_it()
    {
        var (tasks, id) = await ADescribedListAsync();

        await ATaskListHandler(tasks).HandleAsync(
            new UpdateTaskListCommand(UserId, id, "Zakupy", [], IsGroup: false, IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        Assert.Equal("Na sobotę", (await tasks.GetByIdAsync(UserId, id, CancellationToken.None))!.Description);
    }

    /// <summary>An empty string is somebody clearing it, which is not the same as not mentioning it.</summary>
    [Fact]
    public async Task A_save_that_sends_an_empty_description_clears_it()
    {
        var (tasks, id) = await ADescribedListAsync();

        await ATaskListHandler(tasks).HandleAsync(
            new UpdateTaskListCommand(
                UserId, id, "Zakupy", [], IsGroup: false, IsPrivate: false, EncryptedContent: null,
                Description: string.Empty),
            CancellationToken.None);

        Assert.Empty((await tasks.GetByIdAsync(UserId, id, CancellationToken.None))!.Description);
    }

    [Fact]
    public async Task A_warehouse_save_that_says_nothing_about_the_description_keeps_it()
    {
        var context = new InventoryTestContext();
        var warehouse = Warehouse.Create(UserId, "Spiżarnia", description: "Za lodówką");
        await context.WarehouseRepository.AddAsync(warehouse, CancellationToken.None);

        await AWarehouseHandler(context).HandleAsync(
            new UpdateWarehouseCommand(UserId, warehouse.Id, "Spiżarnia", [], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        Assert.Equal(
            "Za lodówką",
            (await context.WarehouseRepository.GetByIdAsync(UserId, warehouse.Id, CancellationToken.None))!.Description);
    }

    /// <summary>
    /// And the same for a shelf item's flag. This one matters most: a warehouse save sends the whole
    /// list back, so an older client returns every item without the flag at once.
    /// </summary>
    [Fact]
    public async Task A_warehouse_save_that_says_nothing_about_an_item_keeps_it_being_checked()
    {
        var context = new InventoryTestContext();
        var warehouse = Warehouse.Create(UserId, "Spiżarnia");
        await context.WarehouseRepository.AddAsync(warehouse, CancellationToken.None);
        var item = InventoryItem.Create(
            warehouse.Id, "Mleko", "Nabiał", "Jedzenie", 1, null, InventoryUnit.Piece, null,
            NotificationChannel.None, isCheckedRegularly: true);
        await context.InventoryRepository.AddAsync(item, CancellationToken.None);

        await AWarehouseHandler(context).HandleAsync(
            new UpdateWarehouseCommand(
                UserId, warehouse.Id, "Spiżarnia",
                [new WarehouseItemInput(
                    item.Id, "Mleko", "Nabiał", "Jedzenie", 1, null, InventoryUnit.Piece, null,
                    NotificationChannel.None)],
                IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var saved = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouse.Id, CancellationToken.None));
        Assert.True(saved.IsCheckedRegularly);
    }

    [Fact]
    public async Task A_warehouse_save_that_says_false_turns_it_off()
    {
        var context = new InventoryTestContext();
        var warehouse = Warehouse.Create(UserId, "Spiżarnia");
        await context.WarehouseRepository.AddAsync(warehouse, CancellationToken.None);
        var item = InventoryItem.Create(
            warehouse.Id, "Mleko", "Nabiał", "Jedzenie", 1, null, InventoryUnit.Piece, null,
            NotificationChannel.None, isCheckedRegularly: true);
        await context.InventoryRepository.AddAsync(item, CancellationToken.None);

        await AWarehouseHandler(context).HandleAsync(
            new UpdateWarehouseCommand(
                UserId, warehouse.Id, "Spiżarnia",
                [new WarehouseItemInput(
                    item.Id, "Mleko", "Nabiał", "Jedzenie", 1, null, InventoryUnit.Piece, null,
                    NotificationChannel.None, IsCheckedRegularly: false)],
                IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var saved = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouse.Id, CancellationToken.None));
        Assert.False(saved.IsCheckedRegularly);
    }

    private static UpdateWarehouseCommandHandler AWarehouseHandler(InventoryTestContext context)
        => new(
            new WarehouseAccessResolver(context.WarehouseRepository, new InMemoryWarehouseShareRepository(), new InMemoryUserRepository()),
            context.WarehouseRepository, context.InventoryRepository, context.TaskListCoordinator);
}

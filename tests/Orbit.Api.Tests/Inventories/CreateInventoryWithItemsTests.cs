using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.CreateInventory;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// Creating an inventory that already holds rows. It used to be refused - the endpoint threw when Items
/// was not empty, on the grounds that the create path would have dropped them - and /inventory/new is a
/// name-it-and-fill-it screen, so naming an inventory, adding a row and pressing Save was a save that
/// could never succeed. The rows now go through the same InventoryItemsSaver a save uses, which is what
/// these hold: not merely that they are stored, but that they are stored the way a save would store them.
/// </summary>
public sealed class CreateInventoryWithItemsTests
{
    [Fact]
    public async Task An_inventory_can_be_created_with_what_is_already_on_the_shelf()
    {
        var context = new InventoryTestContext();
        var ownerId = Guid.NewGuid();

        var inventoryId = await CreateAsync(
            context, ownerId, [Item("Flour"), Item("Sugar")]);

        var stored = await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None);
        Assert.Equal(["Flour", "Sugar"], stored.Select(item => item.Name));
    }

    [Fact]
    public async Task The_order_the_rows_arrive_in_is_the_order_the_shelf_keeps()
    {
        var context = new InventoryTestContext();
        var ownerId = Guid.NewGuid();

        var inventoryId = await CreateAsync(
            context, ownerId, [Item("Sugar"), Item("Flour"), Item("Salt")]);

        // The order somebody arranged them in on screen - see InventoryItem.Position.
        var stored = await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None);
        Assert.Equal([0, 1, 2], stored.Select(item => item.Position));
        Assert.Equal(["Sugar", "Flour", "Salt"], stored.Select(item => item.Name));
    }

    /// <summary>
    /// The standing "keep your stock updated" reminder exists from the first item an inventory ever
    /// holds. Going through the same saver is what gets this for free; a create path that wrote rows of
    /// its own would have had to remember it.
    /// </summary>
    [Fact]
    public async Task An_inventory_created_with_items_gets_its_managed_task_list()
    {
        var context = new InventoryTestContext();
        var ownerId = Guid.NewGuid();

        var inventoryId = await CreateAsync(context, ownerId, [Item("Flour")]);

        Assert.NotNull(await context.ManagedTaskListRepository.GetTaskListIdAsync(inventoryId, CancellationToken.None));
    }

    [Fact]
    public async Task An_inventory_created_with_nothing_on_it_raises_no_list()
    {
        var context = new InventoryTestContext();
        var ownerId = Guid.NewGuid();

        var inventoryId = await CreateAsync(context, ownerId, []);

        Assert.Empty(await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None));
        Assert.Null(await context.ManagedTaskListRepository.GetTaskListIdAsync(inventoryId, CancellationToken.None));
    }

    /// <summary>
    /// A private inventory keeps no readable rows at all - what it holds is sealed inside its payload.
    /// The browser already sends an empty list for one; this is what makes that a rule rather than the
    /// client's good manners.
    /// </summary>
    [Fact]
    public async Task A_private_inventory_created_with_items_still_keeps_no_rows()
    {
        var context = new InventoryTestContext();
        var ownerId = Guid.NewGuid();

        var inventoryId = await new CreateInventoryCommandHandler(context.InventoryRepository, context.ItemsSaver)
            .HandleAsync(
                new CreateInventoryCommand(
                    ownerId, string.Empty, IsPrivate: true, new EncryptedPayload("c2VhbGVk", "bm9uY2U="),
                    Description: null, Items: [Item("Flour")]),
                CancellationToken.None);

        Assert.Empty(await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None));
    }

    private static Task<Guid> CreateAsync(
        InventoryTestContext context, Guid ownerId, IReadOnlyList<InventoryItemInput> items)
        => new CreateInventoryCommandHandler(context.InventoryRepository, context.ItemsSaver)
            .HandleAsync(
                new CreateInventoryCommand(ownerId, "Pantry", Items: items), CancellationToken.None);

    private static InventoryItemInput Item(string name)
        => new(Id: null, name, "Food", "Dry", 1, null, InventoryUnit.Piece, null, NotificationChannel.None);
}

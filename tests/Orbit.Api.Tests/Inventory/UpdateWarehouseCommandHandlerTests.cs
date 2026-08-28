using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.AcquireWarehouseLock;
using Orbit.Core.Inventory.UpdateWarehouse;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

/// <summary>
/// The warehouse editor saves the name and the whole item list in one request, so these cover the
/// create/update/delete reconciliation that replaced the old per-item handlers, plus the edit lock.
/// </summary>
public sealed class UpdateWarehouseCommandHandlerTests
{
    [Fact]
    public async Task HandleAsync_adds_items_that_arrive_without_an_id()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        var outcome = await CreateHandler(context).HandleAsync(
            new UpdateWarehouseCommand(userId, warehouseId, "Kitchen", [NewItem("Milk", quantity: 5m)], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var stored = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None));
        Assert.Equal("Milk", stored.Name);
    }

    [Fact]
    public async Task HandleAsync_updates_an_existing_item_in_place_keeping_its_id()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var existing = await AddStoredItemAsync(context, warehouseId, "Milk", quantity: 5m);

        await CreateHandler(context).HandleAsync(
            new UpdateWarehouseCommand(userId, warehouseId, "Kitchen", [NewItem("Whole milk", quantity: 3m) with { Id = existing.Id }], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var stored = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None));
        Assert.Equal(existing.Id, stored.Id);
        Assert.Equal("Whole milk", stored.Name);
        Assert.Equal(3m, stored.Quantity);
    }

    [Fact]
    public async Task HandleAsync_deletes_items_left_out_of_the_saved_list()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var kept = await AddStoredItemAsync(context, warehouseId, "Milk", quantity: 5m);
        await AddStoredItemAsync(context, warehouseId, "Eggs", quantity: 5m);

        await CreateHandler(context).HandleAsync(
            new UpdateWarehouseCommand(userId, warehouseId, "Kitchen", [NewItem("Milk", quantity: 5m) with { Id = kept.Id }], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var stored = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None));
        Assert.Equal("Milk", stored.Name);
    }

    [Fact]
    public async Task HandleAsync_raises_a_restock_task_for_an_item_saved_below_its_minimum()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        await CreateHandler(context).HandleAsync(
            new UpdateWarehouseCommand(userId, warehouseId, "Kitchen", [NewItem("Milk", quantity: 0m, minimumQuantity: 1m)], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var stored = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None));
        Assert.NotNull(stored.PendingRestockTaskItemId);
        var taskList = await context.TaskRepository.GetByIdAsync(userId, stored.PendingRestockTaskListId!.Value, CancellationToken.None);
        Assert.Contains(taskList!.Items, item => RestockTaskNaming.ProductIn(item.Description) == "Milk");
    }

    [Fact]
    public async Task HandleAsync_raises_no_restock_task_for_an_item_sitting_exactly_at_its_minimum()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        await CreateHandler(context).HandleAsync(
            new UpdateWarehouseCommand(userId, warehouseId, "Kitchen", [NewItem("Milk", quantity: 1m, minimumQuantity: 1m)], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        // The minimum is the level to keep, not one that already needs restocking - 1 of 1 is fine.
        var stored = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None));
        Assert.Null(stored.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task HandleAsync_keeps_an_open_restock_task_when_an_item_is_edited_while_still_low()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var handler = CreateHandler(context);
        await handler.HandleAsync(
            new UpdateWarehouseCommand(userId, warehouseId, "Kitchen", [NewItem("Milk", quantity: 0m, minimumQuantity: 1m)], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);
        var afterFirstSave = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None));
        var originalTaskItemId = afterFirstSave.PendingRestockTaskItemId;

        // Renaming a still-low item must not raise a second restock task - this is why items are
        // reconciled by id rather than deleted and re-created on every save.
        await handler.HandleAsync(
            new UpdateWarehouseCommand(
                userId, warehouseId, "Kitchen",
                [NewItem("Whole milk", quantity: 0m, minimumQuantity: 1m) with { Id = afterFirstSave.Id }], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var stored = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None));
        Assert.Equal(originalTaskItemId, stored.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task HandleAsync_clears_the_restock_reference_once_an_item_is_saved_back_above_its_minimum()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var handler = CreateHandler(context);
        await handler.HandleAsync(
            new UpdateWarehouseCommand(userId, warehouseId, "Kitchen", [NewItem("Milk", quantity: 0m, minimumQuantity: 1m)], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);
        var low = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None));

        await handler.HandleAsync(
            new UpdateWarehouseCommand(
                userId, warehouseId, "Kitchen", [NewItem("Milk", quantity: 9m, minimumQuantity: 1m) with { Id = low.Id }], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var stored = Assert.Single(await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None));
        Assert.Null(stored.PendingRestockTaskListId);
        Assert.Null(stored.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task HandleAsync_returns_locked_while_someone_else_holds_the_edit_lock()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        context.AddAcceptedShare(warehouseId, ownerUserId, otherUserId, ShareAccessLevel.CanEdit);
        context.AddUser(otherUserId, "otheruser");
        await AcquireLockAsync(context, otherUserId, warehouseId);

        var outcome = await CreateHandler(context).HandleAsync(
            new UpdateWarehouseCommand(ownerUserId, warehouseId, "Kitchen", [], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Locked, outcome.Kind);
        Assert.Equal("otheruser", outcome.LockedByUserName);
    }

    [Fact]
    public async Task HandleAsync_lets_the_lock_holder_keep_saving()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        context.AddUser(userId, "owner");
        await AcquireLockAsync(context, userId, warehouseId);

        var outcome = await CreateHandler(context).HandleAsync(
            new UpdateWarehouseCommand(userId, warehouseId, "Kitchen", [], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
    }

    [Fact]
    public async Task HandleAsync_keeps_the_shelf_in_the_order_the_items_arrive_in()
    {
        // The editor sends its rows in the order somebody arranged them, so that is the order the shelf
        // is stored and read back in - not the alphabetical one it would fall back to.
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);

        await CreateHandler(context).HandleAsync(
            new UpdateWarehouseCommand(
                userId, warehouseId, "Kitchen",
                [NewItem("Sugar", 1m), NewItem("Flour", 1m), NewItem("Milk", 1m)], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var stored = await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None);
        Assert.Equal(["Sugar", "Flour", "Milk"], stored.Select(item => item.Name));
        Assert.Equal([0, 1, 2], stored.Select(item => item.Position));
    }

    [Fact]
    public async Task HandleAsync_moves_an_item_that_comes_back_in_a_different_place()
    {
        var context = new InventoryTestContext();
        var userId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(userId);
        var sugar = await AddStoredItemAsync(context, warehouseId, "Sugar", quantity: 1m);
        var flour = await AddStoredItemAsync(context, warehouseId, "Flour", quantity: 1m);

        await CreateHandler(context).HandleAsync(
            new UpdateWarehouseCommand(
                userId, warehouseId, "Kitchen",
                [NewItem("Flour", 1m) with { Id = flour.Id }, NewItem("Sugar", 1m) with { Id = sugar.Id }],
                IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        var stored = await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None);
        Assert.Equal([flour.Id, sugar.Id], stored.Select(item => item.Id));
    }

    private static UpdateWarehouseCommandHandler CreateHandler(InventoryTestContext context)
        => new(context.AccessResolver, context.WarehouseRepository, context.InventoryRepository, context.TaskListCoordinator);

    private static Task<EditOutcome> AcquireLockAsync(InventoryTestContext context, Guid userId, Guid warehouseId)
        => new AcquireWarehouseLockCommandHandler(context.AccessResolver, context.WarehouseRepository, context.UserRepository)
            .HandleAsync(new AcquireWarehouseLockCommand(userId, warehouseId), CancellationToken.None);

    private static WarehouseItemInput NewItem(string name, decimal quantity, decimal? minimumQuantity = null)
        => new(Id: null, name, "Dairy", "Fridge", quantity, minimumQuantity, ExpiryDate: null, NotificationChannel.Push);

    private static async Task<InventoryItem> AddStoredItemAsync(
        InventoryTestContext context, Guid warehouseId, string name, decimal quantity)
    {
        var item = InventoryItem.Create(warehouseId, name, "Dairy", "Fridge", quantity, null, null, NotificationChannel.Push);
        await context.InventoryRepository.AddAsync(item, CancellationToken.None);
        return item;
    }
}

using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Inventory.AcceptWarehouseShare;
using Orbit.Core.Inventory.DeleteWarehouse;
using Orbit.Core.Inventory.GetWarehouses;
using Orbit.Core.Inventory.ShareWarehouse;
using Orbit.Core.Inventory.UpdateWarehouse;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

/// <summary>
/// Covers the sharing rules warehouses inherit from the Notes pattern, plus the two things unique to
/// warehouses: access to a warehouse *is* access to its items, and deleting one takes its items with it.
/// </summary>
public sealed class WarehouseSharingTests
{
    [Fact]
    public async Task Sharing_then_accepting_makes_the_warehouse_show_up_for_the_recipient()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId, "Kitchen");

        var outcome = await ShareAsync(context, ownerUserId, warehouseId, recipientUserId, ShareAccessLevel.ReadOnly);
        Assert.NotNull(outcome);
        Assert.False(outcome!.AlreadyShared);

        // Not visible until accepted - a pending offer grants nothing.
        Assert.Empty(await ListWarehousesAsync(context, recipientUserId));

        var accepted = await new AcceptWarehouseShareCommandHandler(context.WarehouseShareRepository)
            .HandleAsync(new AcceptWarehouseShareCommand(recipientUserId, outcome.ShareId), CancellationToken.None);
        Assert.True(accepted);

        var visible = Assert.Single(await ListWarehousesAsync(context, recipientUserId));
        Assert.Equal(warehouseId, visible.Id);
        Assert.True(visible.IsShared);
        Assert.Equal(ShareAccessLevel.ReadOnly, visible.AccessLevel);
    }

    [Fact]
    public async Task Sharing_a_warehouse_tells_the_recipient_about_it()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId, "Kitchen");
        var sharedItemNotifier = new RecordingSharedItemNotifier();

        await ShareAsync(context, ownerUserId, warehouseId, recipientUserId, ShareAccessLevel.ReadOnly, sharedItemNotifier);

        var announcement = Assert.Single(sharedItemNotifier.Announced);
        Assert.Equal(recipientUserId, announcement.RecipientUserId);
        Assert.Equal(ownerUserId, announcement.SharerUserId);
        Assert.Equal(SharedItemKind.Warehouse, announcement.Kind);
        Assert.Equal("Kitchen", announcement.ItemTitle);
    }

    [Fact]
    public async Task Sharing_the_same_warehouse_twice_reuses_the_existing_offer()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);

        var first = await ShareAsync(context, ownerUserId, warehouseId, recipientUserId, ShareAccessLevel.ReadOnly);
        var second = await ShareAsync(context, ownerUserId, warehouseId, recipientUserId, ShareAccessLevel.ReadOnly);

        Assert.Equal(first!.ShareId, second!.ShareId);
        Assert.True(second.AlreadyShared);
    }

    [Fact]
    public async Task A_read_only_recipient_cannot_re_share()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var thirdUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        context.AddAcceptedShare(warehouseId, ownerUserId, recipientUserId, ShareAccessLevel.ReadOnly);

        var outcome = await ShareAsync(context, recipientUserId, warehouseId, thirdUserId, ShareAccessLevel.ReadOnly);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task A_recipient_cannot_re_share_above_their_own_level()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var thirdUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        context.AddAcceptedShare(warehouseId, ownerUserId, recipientUserId, ShareAccessLevel.Share);

        var tooHigh = await ShareAsync(context, recipientUserId, warehouseId, thirdUserId, ShareAccessLevel.CanEdit);
        Assert.Null(tooHigh);

        var atOwnLevel = await ShareAsync(context, recipientUserId, warehouseId, thirdUserId, ShareAccessLevel.Share);
        Assert.NotNull(atOwnLevel);
    }

    [Fact]
    public async Task Nobody_can_share_a_warehouse_back_to_its_owner()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);

        var outcome = await ShareAsync(context, ownerUserId, warehouseId, ownerUserId, ShareAccessLevel.ReadOnly);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task A_read_only_recipient_cannot_rename_the_warehouse()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId, "Kitchen");
        context.AddAcceptedShare(warehouseId, ownerUserId, recipientUserId, ShareAccessLevel.ReadOnly);
        var handler = new UpdateWarehouseCommandHandler(
            context.AccessResolver, context.WarehouseRepository, context.InventoryRepository, context.TaskListCoordinator);

        var outcome = await handler.HandleAsync(
            new UpdateWarehouseCommand(recipientUserId, warehouseId, "Renamed", [], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }

    [Fact]
    public async Task A_can_edit_recipient_cannot_delete_the_warehouse()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        context.AddAcceptedShare(warehouseId, ownerUserId, recipientUserId, ShareAccessLevel.CanEdit);
        var handler = new DeleteWarehouseCommandHandler(context.WarehouseRepository, context.InventoryRepository, new InMemoryWarehouseShareRepository());

        var deleted = await handler.HandleAsync(new DeleteWarehouseCommand(recipientUserId, warehouseId), CancellationToken.None);

        Assert.False(deleted);
        Assert.NotNull(await context.WarehouseRepository.GetByIdAsync(ownerUserId, warehouseId, CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_a_warehouse_takes_its_items_with_it()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        await context.InventoryRepository.AddAsync(
            InventoryItem.Create(warehouseId, "Milk", "Dairy", "Fridge", 2m, 1m, null, NotificationChannel.Push), CancellationToken.None);
        var handler = new DeleteWarehouseCommandHandler(context.WarehouseRepository, context.InventoryRepository, new InMemoryWarehouseShareRepository());

        var deleted = await handler.HandleAsync(new DeleteWarehouseCommand(ownerUserId, warehouseId), CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(await context.InventoryRepository.GetAllAsync(warehouseId, CancellationToken.None));
    }

    [Fact]
    public async Task A_deleted_warehouse_disappears_for_its_share_recipients_too()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var warehouseId = context.AddWarehouse(ownerUserId);
        context.AddAcceptedShare(warehouseId, ownerUserId, recipientUserId, ShareAccessLevel.CanEdit);
        Assert.Single(await ListWarehousesAsync(context, recipientUserId));

        await new DeleteWarehouseCommandHandler(context.WarehouseRepository, context.InventoryRepository, new InMemoryWarehouseShareRepository())
            .HandleAsync(new DeleteWarehouseCommand(ownerUserId, warehouseId), CancellationToken.None);

        // The grant row is left behind deliberately; the resolver reads it as "not found".
        Assert.Empty(await ListWarehousesAsync(context, recipientUserId));
    }

    private static Task<ShareOutcome?> ShareAsync(
        InventoryTestContext context, Guid callerId, Guid warehouseId, Guid recipientUserId, ShareAccessLevel accessLevel,
        RecordingSharedItemNotifier? sharedItemNotifier = null)
        => new ShareWarehouseCommandHandler(
                context.AccessResolver, context.WarehouseShareRepository, sharedItemNotifier ?? new RecordingSharedItemNotifier())
            .HandleAsync(new ShareWarehouseCommand(callerId, warehouseId, recipientUserId, accessLevel), CancellationToken.None);

    private static Task<IReadOnlyList<Warehouse>> ListWarehousesAsync(InventoryTestContext context, Guid userId)
        => new GetWarehousesQueryHandler(context.AccessResolver).HandleAsync(new GetWarehousesQuery(userId), CancellationToken.None);
}

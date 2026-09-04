using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.AcceptInventoryShare;
using Orbit.Core.Inventories.DeleteInventory;
using Orbit.Core.Inventories.GetInventories;
using Orbit.Core.Inventories.ShareInventory;
using Orbit.Core.Inventories.UpdateInventory;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// Covers the sharing rules inventories inherit from the Notes pattern, plus the two things unique to
/// inventories: access to an inventory *is* access to its items, and deleting one takes its items with it.
/// </summary>
public sealed class InventorySharingTests
{
    [Fact]
    public async Task Sharing_then_accepting_makes_the_inventory_show_up_for_the_recipient()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId, "Kitchen");

        var outcome = await ShareAsync(context, ownerUserId, inventoryId, recipientUserId, ShareAccessLevel.ReadOnly);
        Assert.NotNull(outcome);
        Assert.False(outcome!.AlreadyShared);

        // Not visible until accepted - a pending offer grants nothing.
        Assert.Empty(await ListInventoriesAsync(context, recipientUserId));

        var accepted = await new AcceptInventoryShareCommandHandler(context.InventoryShareRepository)
            .HandleAsync(new AcceptInventoryShareCommand(recipientUserId, outcome.ShareId), CancellationToken.None);
        Assert.True(accepted);

        var visible = Assert.Single(await ListInventoriesAsync(context, recipientUserId));
        Assert.Equal(inventoryId, visible.Id);
        Assert.True(visible.IsShared);
        Assert.Equal(ShareAccessLevel.ReadOnly, visible.AccessLevel);
    }

    [Fact]
    public async Task Sharing_an_inventory_tells_the_recipient_about_it()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId, "Kitchen");
        var sharedItemNotifier = new RecordingSharedItemNotifier();

        await ShareAsync(context, ownerUserId, inventoryId, recipientUserId, ShareAccessLevel.ReadOnly, sharedItemNotifier);

        var announcement = Assert.Single(sharedItemNotifier.Announced);
        Assert.Equal(recipientUserId, announcement.RecipientUserId);
        Assert.Equal(ownerUserId, announcement.SharerUserId);
        Assert.Equal(SharedItemKind.Inventory, announcement.Kind);
        Assert.Equal("Kitchen", announcement.ItemTitle);
    }

    [Fact]
    public async Task Sharing_the_same_inventory_twice_reuses_the_existing_offer()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId);

        var first = await ShareAsync(context, ownerUserId, inventoryId, recipientUserId, ShareAccessLevel.ReadOnly);
        var second = await ShareAsync(context, ownerUserId, inventoryId, recipientUserId, ShareAccessLevel.ReadOnly);

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
        var inventoryId = context.AddInventory(ownerUserId);
        context.AddAcceptedShare(inventoryId, ownerUserId, recipientUserId, ShareAccessLevel.ReadOnly);

        var outcome = await ShareAsync(context, recipientUserId, inventoryId, thirdUserId, ShareAccessLevel.ReadOnly);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task A_recipient_cannot_re_share_above_their_own_level()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var thirdUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId);
        context.AddAcceptedShare(inventoryId, ownerUserId, recipientUserId, ShareAccessLevel.Share);

        var tooHigh = await ShareAsync(context, recipientUserId, inventoryId, thirdUserId, ShareAccessLevel.CanEdit);
        Assert.Null(tooHigh);

        var atOwnLevel = await ShareAsync(context, recipientUserId, inventoryId, thirdUserId, ShareAccessLevel.Share);
        Assert.NotNull(atOwnLevel);
    }

    [Fact]
    public async Task Nobody_can_share_an_inventory_back_to_its_owner()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId);

        var outcome = await ShareAsync(context, ownerUserId, inventoryId, ownerUserId, ShareAccessLevel.ReadOnly);

        Assert.Null(outcome);
    }

    [Fact]
    public async Task A_read_only_recipient_cannot_rename_the_inventory()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId, "Kitchen");
        context.AddAcceptedShare(inventoryId, ownerUserId, recipientUserId, ShareAccessLevel.ReadOnly);
        var handler = new UpdateInventoryCommandHandler(
            context.AccessResolver, context.InventoryRepository, context.ItemsSaver);

        var outcome = await handler.HandleAsync(
            new UpdateInventoryCommand(recipientUserId, inventoryId, "Renamed", [], IsPrivate: false, EncryptedContent: null), CancellationToken.None);

        Assert.Equal(EditOutcomeKind.ReadOnly, outcome.Kind);
    }

    [Fact]
    public async Task A_can_edit_recipient_cannot_delete_the_inventory()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId);
        context.AddAcceptedShare(inventoryId, ownerUserId, recipientUserId, ShareAccessLevel.CanEdit);
        var handler = new DeleteInventoryCommandHandler(context.InventoryRepository, context.InventoryItemRepository, new InMemoryInventoryShareRepository(), new InMemorySyncTombstoneRepository());

        var deleted = await handler.HandleAsync(new DeleteInventoryCommand(recipientUserId, inventoryId), CancellationToken.None);

        Assert.False(deleted);
        Assert.NotNull(await context.InventoryRepository.GetByIdAsync(ownerUserId, inventoryId, CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_an_inventory_takes_its_items_with_it()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId);
        await context.InventoryItemRepository.AddAsync(
            InventoryItem.Create(inventoryId, "Milk", "Dairy", ["Fridge"], 2m, 1m, InventoryUnit.Piece, null, NotificationChannel.Push), CancellationToken.None);
        var handler = new DeleteInventoryCommandHandler(context.InventoryRepository, context.InventoryItemRepository, new InMemoryInventoryShareRepository(), new InMemorySyncTombstoneRepository());

        var deleted = await handler.HandleAsync(new DeleteInventoryCommand(ownerUserId, inventoryId), CancellationToken.None);

        Assert.True(deleted);
        Assert.Empty(await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None));
    }

    [Fact]
    public async Task A_deleted_inventory_disappears_for_its_share_recipients_too()
    {
        var context = new InventoryTestContext();
        var ownerUserId = Guid.NewGuid();
        var recipientUserId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerUserId);
        context.AddAcceptedShare(inventoryId, ownerUserId, recipientUserId, ShareAccessLevel.CanEdit);
        Assert.Single(await ListInventoriesAsync(context, recipientUserId));

        await new DeleteInventoryCommandHandler(context.InventoryRepository, context.InventoryItemRepository, new InMemoryInventoryShareRepository(), new InMemorySyncTombstoneRepository())
            .HandleAsync(new DeleteInventoryCommand(ownerUserId, inventoryId), CancellationToken.None);

        // The grant row is left behind deliberately; the resolver reads it as "not found".
        Assert.Empty(await ListInventoriesAsync(context, recipientUserId));
    }

    private static Task<ShareOutcome?> ShareAsync(
        InventoryTestContext context, Guid callerId, Guid inventoryId, Guid recipientUserId, ShareAccessLevel accessLevel,
        RecordingSharedItemNotifier? sharedItemNotifier = null)
        => new ShareInventoryCommandHandler(
                context.AccessResolver, context.InventoryShareRepository, sharedItemNotifier ?? new RecordingSharedItemNotifier())
            .HandleAsync(new ShareInventoryCommand(callerId, inventoryId, recipientUserId, accessLevel), CancellationToken.None);

    private static Task<IReadOnlyList<Inventory>> ListInventoriesAsync(InventoryTestContext context, Guid userId)
        => new GetInventoriesQueryHandler(context.AccessResolver).HandleAsync(new GetInventoriesQuery(userId), CancellationToken.None);
}

using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Inventories.CreateInventory;
using Orbit.Core.Inventories.ShareInventory;
using Orbit.Core.Inventories.UpdateInventory;
using Orbit.Core.Notifications;
using Xunit;

namespace Orbit.Api.Tests.Inventories;

/// <summary>
/// Covers what a private inventory promises. Unlike a note or a task list, its items are rows of their
/// own, so "the server can't read this" has to mean those rows are gone - not merely that the name is
/// sealed. Everything below is about checking that literally.
/// </summary>
public sealed class PrivateInventoryTests
{
    private static readonly EncryptedPayload SealedContent = new("c2VhbGVk", "bm9uY2U=");

    [Fact]
    public async Task A_private_inventory_keeps_no_readable_name()
    {
        var context = new PrivateInventoryTestContext();

        var inventoryId = await context.CreateAsync("Medicine cabinet", isPrivate: true, SealedContent);

        var stored = await context.InventoryRepository.GetByIdAsync(context.OwnerId, inventoryId, CancellationToken.None);
        Assert.Equal(string.Empty, stored!.Name);
        Assert.Equal(SealedContent, stored.EncryptedContent);
        Assert.True(stored.IsPrivate);
    }

    [Fact]
    public async Task Claiming_privacy_without_sealed_content_is_refused()
    {
        var context = new PrivateInventoryTestContext();

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => context.CreateAsync("Medicine cabinet", isPrivate: true, encryptedContent: null));
    }

    [Fact]
    public async Task Saving_a_private_inventory_stores_no_item_rows()
    {
        var context = new PrivateInventoryTestContext();
        var inventoryId = await context.CreateAsync("Medicine cabinet", isPrivate: true, SealedContent);

        // The caller sends its items anyway - the client seals them and empties the list, but a
        // hand-made request could still carry them, and they must not land.
        await context.SaveAsync(inventoryId, "Medicine cabinet", [Item("Painkillers", 2, 5)], isPrivate: true, SealedContent);

        Assert.Empty(await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None));
    }

    [Fact]
    public async Task Turning_privacy_on_removes_the_item_rows_that_were_there_before()
    {
        var context = new PrivateInventoryTestContext();
        var inventoryId = await context.CreateAsync("Pantry", isPrivate: false, encryptedContent: null);
        await context.SaveAsync(inventoryId, "Pantry", [Item("Flour", 2, 1), Item("Sugar", 0, 2)], isPrivate: false, encryptedContent: null);
        Assert.Equal(2, (await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None)).Count);

        await context.SaveAsync(inventoryId, "Pantry", [], isPrivate: true, SealedContent);

        // This is the whole promise: not that the rows are hidden, but that they are gone.
        Assert.Empty(await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None));
    }

    [Fact]
    public async Task A_private_inventory_raises_no_restock_task_for_an_item_below_its_minimum()
    {
        var context = new PrivateInventoryTestContext();
        var inventoryId = await context.CreateAsync("Medicine cabinet", isPrivate: true, SealedContent);

        await context.SaveAsync(inventoryId, "Medicine cabinet", [Item("Painkillers", 0, 5)], isPrivate: true, SealedContent);

        // There is no item row to notice, which is the cost of the server not being able to read one.
        Assert.Empty(await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None));
        Assert.Empty(await context.TaskRepository.GetAllAsync(context.OwnerId, updatedSinceUtc: null, CancellationToken.None));
    }

    [Fact]
    public async Task An_ordinary_inventory_still_raises_one()
    {
        // The control: without it, "no restock task" above could just as well mean the fixture never
        // raises any.
        var context = new PrivateInventoryTestContext();
        var inventoryId = await context.CreateAsync("Pantry", isPrivate: false, encryptedContent: null);

        await context.SaveAsync(inventoryId, "Pantry", [Item("Flour", 0, 1)], isPrivate: false, encryptedContent: null);

        var stored = Assert.Single(await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None));
        Assert.True(stored.IsBelowMinimum);
        Assert.NotNull(stored.PendingRestockTaskItemId);
    }

    [Fact]
    public async Task Turning_privacy_back_off_leaves_a_readable_inventory_with_no_sealed_content()
    {
        var context = new PrivateInventoryTestContext();
        var inventoryId = await context.CreateAsync("Medicine cabinet", isPrivate: true, SealedContent);

        await context.SaveAsync(inventoryId, "Pantry", [Item("Flour", 2, 1)], isPrivate: false, encryptedContent: null);

        var stored = await context.InventoryRepository.GetByIdAsync(context.OwnerId, inventoryId, CancellationToken.None);
        Assert.Equal("Pantry", stored!.Name);
        Assert.Null(stored.EncryptedContent);
        Assert.False(stored.IsPrivate);
        Assert.Single(await context.InventoryItemRepository.GetAllAsync(inventoryId, CancellationToken.None));
    }

    [Fact]
    public async Task A_private_inventory_cannot_be_shared()
    {
        var context = new PrivateInventoryTestContext();
        var inventoryId = await context.CreateAsync("Medicine cabinet", isPrivate: true, SealedContent);

        await Assert.ThrowsAsync<InvalidRequestException>(() => context.ShareAsync(inventoryId, Guid.NewGuid()));
    }

    [Fact]
    public async Task An_ordinary_shared_inventory_still_resolves_for_its_recipient()
    {
        // The control for the test below - see PrivateNoteTests for why it is its own test.
        var context = new PrivateInventoryTestContext();
        var recipientId = Guid.NewGuid();
        var inventoryId = await context.CreateAsync("Pantry", isPrivate: false, encryptedContent: null);
        await context.ShareAndAcceptAsync(inventoryId, recipientId);

        Assert.NotNull(await context.ResolveForAsync(recipientId, inventoryId));
    }

    [Fact]
    public async Task An_existing_share_stops_granting_access_once_the_inventory_becomes_private()
    {
        var context = new PrivateInventoryTestContext();
        var recipientId = Guid.NewGuid();
        var inventoryId = await context.CreateAsync("Pantry", isPrivate: false, encryptedContent: null);
        await context.ShareAndAcceptAsync(inventoryId, recipientId);

        await context.SaveAsync(inventoryId, "Pantry", [], isPrivate: true, SealedContent);

        Assert.Null(await context.ResolveForAsync(recipientId, inventoryId));
        Assert.NotNull(await context.ResolveForAsync(context.OwnerId, inventoryId));
    }

    private static InventoryItemInput Item(string name, decimal quantity, decimal? minimumQuantity)
        => new(null, name, "Medicine", "Cabinet", quantity, minimumQuantity, InventoryUnit.Piece, null, NotificationChannel.None);

    /// <summary>
    /// Wraps the shared InventoryTestContext with the three calls these tests make, rather than wiring
    /// the collaborator graph a second time.
    /// </summary>
    private sealed class PrivateInventoryTestContext
    {
        private readonly InventoryTestContext _inventory = new();

        public InMemoryInventoryRepository InventoryRepository => _inventory.InventoryRepository;
        public InMemoryInventoryItemRepository InventoryItemRepository => _inventory.InventoryItemRepository;
        public InMemoryTaskRepository TaskRepository => _inventory.TaskRepository;
        public Guid OwnerId { get; } = Guid.NewGuid();

        public Task<Guid> CreateAsync(string name, bool isPrivate, EncryptedPayload? encryptedContent)
            => new CreateInventoryCommandHandler(_inventory.InventoryRepository, _inventory.ItemsSaver)
                .HandleAsync(new CreateInventoryCommand(OwnerId, name, isPrivate, encryptedContent), CancellationToken.None);

        public Task<EditOutcome> SaveAsync(
            Guid inventoryId, string name, IReadOnlyList<InventoryItemInput> items, bool isPrivate, EncryptedPayload? encryptedContent)
            => new UpdateInventoryCommandHandler(
                    _inventory.AccessResolver, _inventory.InventoryRepository, _inventory.ItemsSaver)
                .HandleAsync(
                    new UpdateInventoryCommand(OwnerId, inventoryId, name, items, isPrivate, encryptedContent),
                    CancellationToken.None);

        public Task<ShareOutcome?> ShareAsync(Guid inventoryId, Guid recipientId)
            => new ShareInventoryCommandHandler(_inventory.AccessResolver, _inventory.InventoryShareRepository, new RecordingSharedItemNotifier())
                .HandleAsync(new ShareInventoryCommand(OwnerId, inventoryId, recipientId, ShareAccessLevel.ReadOnly), CancellationToken.None);

        public async Task ShareAndAcceptAsync(Guid inventoryId, Guid recipientId)
        {
            var outcome = await ShareAsync(inventoryId, recipientId);
            var share = await _inventory.InventoryShareRepository.GetByIdAsync(recipientId, outcome!.ShareId, CancellationToken.None);
            share!.MarkAccepted();
            await _inventory.InventoryShareRepository.UpdateAsync(share, CancellationToken.None);
        }

        public Task<Inventory?> ResolveForAsync(Guid callerId, Guid inventoryId)
            => _inventory.AccessResolver.ResolveAsync(callerId, inventoryId, CancellationToken.None);
    }
}

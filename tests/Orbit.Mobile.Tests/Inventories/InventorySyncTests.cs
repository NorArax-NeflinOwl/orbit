using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Inventories;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Inventories;

/// <summary>
/// Inventories on the sync spine. Like the calendar's, these cover what this entity type does differently
/// rather than re-checking the spine's own rules: its items arrive by a second call because the change
/// feed does not carry them, and a save means "this is the whole list" rather than "these changed".
/// </summary>
public sealed class InventorySyncTests
{
    [Fact]
    public async Task A_inventory_written_offline_reaches_the_server_when_the_connection_returns()
    {
        using var context = new InventoryContext();
        context.GoOffline();
        await context.Inventories.CreateAsync("Pantry");

        context.ComeBackOnline();
        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.Sent);
        Assert.Contains(context.Server.Inventories, inventory => inventory.Name == "Pantry");
    }

    [Fact]
    public async Task What_a_inventory_holds_is_fetched_even_though_the_change_feed_omits_it()
    {
        using var context = new InventoryContext();
        var remote = context.Server.AddInventory("Pantry");
        context.Server.AddItem(remote.Id, "Flour", 2);

        await context.SynchroniseAsync();

        // InventoryDto carries no items, so without the extra call the phone would show every inventory
        // as empty and a save would then wipe it.
        var stored = (await context.Inventories.GetAllAsync()).Single();
        Assert.Equal("Flour", Assert.Single(stored.Items).Name);
    }

    [Fact]
    public async Task Items_are_only_fetched_for_the_inventories_a_pull_reported()
    {
        using var context = new InventoryContext();
        context.Server.AddInventory("Pantry");
        context.Server.AddInventory("Garage");
        await context.SynchroniseAsync();

        var beforeIdlePull = context.Server.ReceivedRequests.Count(request => request.EndsWith("/items"));
        context.Clock.Advance(TimeSpan.FromMinutes(1));
        await context.SynchroniseAsync();

        // Nothing changed, so nothing needed asking about - the extra calls track what moved rather than
        // how much the user owns.
        Assert.Equal(beforeIdlePull, context.Server.ReceivedRequests.Count(request => request.EndsWith("/items")));
    }

    [Fact]
    public async Task An_item_keeps_its_identity_across_a_save()
    {
        using var context = new InventoryContext();
        var remote = context.Server.AddInventory("Pantry");
        context.Server.AddItem(remote.Id, "Flour", 2);
        await context.SynchroniseAsync();

        var stored = (await context.Inventories.GetAllAsync()).Single();
        var itemId = stored.Items.Single().Id;
        await context.Inventories.UpdateAsync(
            stored.LocalId, new InventoryContent("Pantry", [stored.Items.Single() with { Quantity = 5 }]));
        await context.SynchroniseAsync();

        // Minting a fresh id on every save would cut loose whatever points at the item - an open restock
        // task, an expiry notification - which is the same trap task entries have.
        var onServer = Assert.Single(context.Server.ItemsIn(remote.Id));
        Assert.Equal(itemId, onServer.Id);
        Assert.Equal(5, onServer.Quantity);
    }

    /// <summary>
    /// A row is a batch rather than a product: two rows of one name are two deliveries of it, and when
    /// each arrived is the only thing that tells them apart. The save shape carries no such date - the
    /// server decides it - so the phone keeps it beside the items, and a save must not lose it.
    /// </summary>
    [Fact]
    public async Task A_shelf_remembers_when_each_batch_arrived()
    {
        using var context = new InventoryContext();
        var remote = context.Server.AddInventory("Pantry");
        context.Server.AddItem(remote.Id, "Flour", 2);
        var delivered = context.Clock.GetUtcNow();
        await context.SynchroniseAsync();

        var stored = (await context.Inventories.GetAllAsync()).Single();
        Assert.Equal(delivered, stored.ItemArrivals[stored.Items.Single().Id!.Value]);

        context.Clock.Advance(TimeSpan.FromDays(3));
        await context.Inventories.UpdateAsync(
            stored.LocalId, new InventoryContent("Pantry", [stored.Items.Single() with { Quantity = 5 }]));
        await context.SynchroniseAsync();

        // Counting what is there is not a new delivery: an edited row still arrived when it arrived.
        var afterEditing = (await context.Inventories.GetAllAsync()).Single();
        Assert.Equal(delivered, afterEditing.ItemArrivals[afterEditing.Items.Single().Id!.Value]);
    }

    /// <summary>
    /// The other thing a save must not cut loose. An item asked for every round is on the restock list
    /// whatever its count says, and the phone's save writes the whole list - so a save that said nothing
    /// about the flag would have been read as saying nothing at all, and the item would have gone on
    /// being asked for while nobody could turn it off from here.
    /// </summary>
    [Fact]
    public async Task An_item_asked_for_every_round_stays_that_way_across_a_save()
    {
        using var context = new InventoryContext();
        var remote = context.Server.AddInventory("Pantry");
        context.Server.AddItem(remote.Id, "Flour", 2, isCheckedRegularly: true);
        await context.SynchroniseAsync();

        var stored = (await context.Inventories.GetAllAsync()).Single();
        Assert.True(stored.Items.Single().IsCheckedRegularly);

        await context.Inventories.UpdateAsync(
            stored.LocalId, new InventoryContent("Pantry", [stored.Items.Single() with { Quantity = 5 }]));
        await context.SynchroniseAsync();

        Assert.True(Assert.Single(context.Server.ItemsIn(remote.Id)).IsCheckedRegularly);
    }

    [Fact]
    public async Task Saving_without_an_item_removes_it_because_a_save_is_the_whole_list()
    {
        using var context = new InventoryContext();
        var remote = context.Server.AddInventory("Pantry");
        context.Server.AddItem(remote.Id, "Flour", 2);
        context.Server.AddItem(remote.Id, "Sugar", 1);
        await context.SynchroniseAsync();

        var stored = (await context.Inventories.GetAllAsync()).Single();
        var keeping = stored.Items.Single(item => item.Name == "Flour");
        await context.Inventories.UpdateAsync(stored.LocalId, new InventoryContent("Pantry", [keeping]));
        await context.SynchroniseAsync();

        Assert.Equal("Flour", Assert.Single(context.Server.ItemsIn(remote.Id)).Name);
    }

    [Fact]
    public async Task A_inventory_deleted_elsewhere_leaves_the_phone_too()
    {
        using var context = new InventoryContext();
        var remote = context.Server.AddInventory("Gone");
        await context.SynchroniseAsync();

        context.Clock.Advance(TimeSpan.FromMinutes(1));
        await context.Client.DeleteAsync(remote.Id);
        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.RemovedLocally);
        Assert.Empty(await context.Inventories.GetAllAsync());
    }

    [Fact]
    public async Task Syncing_offline_reports_it_rather_than_throwing()
    {
        using var context = new InventoryContext();
        context.GoOffline();
        await context.Inventories.CreateAsync("Pantry");

        Assert.False((await context.SynchroniseAsync()).ReachedTheServer);
    }

    private sealed class InventoryContext : IDisposable
    {
        private readonly LocalStore _localStore = new();

        public InventoryContext()
        {
            Clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
            Server = new FakeInventoryServer(Clock);
            Client = new InventoryClient(Server.ToHttpClient());
            Inventories = new LocalInventoryRepository(_localStore, Clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            Synchronizer = new InventorySynchronizer(
                _localStore, Client, Clock, new SyncGate(), NullLogger<InventorySynchronizer>.Instance);
        }

        public FakeTimeProvider Clock { get; }
        public FakeInventoryServer Server { get; }
        public InventoryClient Client { get; }
        public LocalInventoryRepository Inventories { get; }
        public InventorySynchronizer Synchronizer { get; }

        public Task<SyncResult> SynchroniseAsync() => Synchronizer.SynchroniseAsync(CancellationToken.None);

        public void GoOffline() => Server.IsUnreachable = true;

        public void ComeBackOnline() => Server.IsUnreachable = false;

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Inventory;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Inventory;

/// <summary>
/// Warehouses on the sync spine. Like the calendar's, these cover what this entity type does differently
/// rather than re-checking the spine's own rules: its items arrive by a second call because the change
/// feed does not carry them, and a save means "this is the whole list" rather than "these changed".
/// </summary>
public sealed class WarehouseSyncTests
{
    [Fact]
    public async Task A_warehouse_written_offline_reaches_the_server_when_the_connection_returns()
    {
        using var context = new WarehouseContext();
        context.GoOffline();
        await context.Warehouses.CreateAsync("Pantry");

        context.ComeBackOnline();
        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.Sent);
        Assert.Contains(context.Server.Warehouses, warehouse => warehouse.Name == "Pantry");
    }

    [Fact]
    public async Task What_a_warehouse_holds_is_fetched_even_though_the_change_feed_omits_it()
    {
        using var context = new WarehouseContext();
        var remote = context.Server.AddWarehouse("Pantry");
        context.Server.AddItem(remote.Id, "Flour", 2);

        await context.SynchroniseAsync();

        // WarehouseDto carries no items, so without the extra call the phone would show every warehouse
        // as empty and a save would then wipe it.
        var stored = (await context.Warehouses.GetAllAsync()).Single();
        Assert.Equal("Flour", Assert.Single(stored.Items).Name);
    }

    [Fact]
    public async Task Items_are_only_fetched_for_the_warehouses_a_pull_reported()
    {
        using var context = new WarehouseContext();
        context.Server.AddWarehouse("Pantry");
        context.Server.AddWarehouse("Garage");
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
        using var context = new WarehouseContext();
        var remote = context.Server.AddWarehouse("Pantry");
        context.Server.AddItem(remote.Id, "Flour", 2);
        await context.SynchroniseAsync();

        var stored = (await context.Warehouses.GetAllAsync()).Single();
        var itemId = stored.Items.Single().Id;
        await context.Warehouses.UpdateAsync(stored.LocalId, "Pantry", [stored.Items.Single() with { Quantity = 5 }]);
        await context.SynchroniseAsync();

        // Minting a fresh id on every save would cut loose whatever points at the item - an open restock
        // task, an expiry notification - which is the same trap task entries have.
        var onServer = Assert.Single(context.Server.ItemsIn(remote.Id));
        Assert.Equal(itemId, onServer.Id);
        Assert.Equal(5, onServer.Quantity);
    }

    [Fact]
    public async Task Saving_without_an_item_removes_it_because_a_save_is_the_whole_list()
    {
        using var context = new WarehouseContext();
        var remote = context.Server.AddWarehouse("Pantry");
        context.Server.AddItem(remote.Id, "Flour", 2);
        context.Server.AddItem(remote.Id, "Sugar", 1);
        await context.SynchroniseAsync();

        var stored = (await context.Warehouses.GetAllAsync()).Single();
        var keeping = stored.Items.Single(item => item.Name == "Flour");
        await context.Warehouses.UpdateAsync(stored.LocalId, "Pantry", [keeping]);
        await context.SynchroniseAsync();

        Assert.Equal("Flour", Assert.Single(context.Server.ItemsIn(remote.Id)).Name);
    }

    [Fact]
    public async Task A_warehouse_deleted_elsewhere_leaves_the_phone_too()
    {
        using var context = new WarehouseContext();
        var remote = context.Server.AddWarehouse("Gone");
        await context.SynchroniseAsync();

        context.Clock.Advance(TimeSpan.FromMinutes(1));
        await context.Client.DeleteAsync(remote.Id);
        var result = await context.SynchroniseAsync();

        Assert.Equal(1, result.RemovedLocally);
        Assert.Empty(await context.Warehouses.GetAllAsync());
    }

    [Fact]
    public async Task Syncing_offline_reports_it_rather_than_throwing()
    {
        using var context = new WarehouseContext();
        context.GoOffline();
        await context.Warehouses.CreateAsync("Pantry");

        Assert.False((await context.SynchroniseAsync()).ReachedTheServer);
    }

    private sealed class WarehouseContext : IDisposable
    {
        private readonly LocalStore _localStore = new();

        public WarehouseContext()
        {
            Clock = new FakeTimeProvider(DateTimeOffset.Parse("2026-08-26T10:00:00Z"));
            Server = new FakeInventoryServer(Clock);
            Client = new InventoryClient(Server.ToHttpClient());
            Warehouses = new LocalWarehouseRepository(_localStore, Clock, FixedNetworkStatus.Online);
            Synchronizer = new WarehouseSynchronizer(
                _localStore, Client, Clock, new SyncGate(), NullLogger<WarehouseSynchronizer>.Instance);
        }

        public FakeTimeProvider Clock { get; }
        public FakeInventoryServer Server { get; }
        public InventoryClient Client { get; }
        public LocalWarehouseRepository Warehouses { get; }
        public WarehouseSynchronizer Synchronizer { get; }

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

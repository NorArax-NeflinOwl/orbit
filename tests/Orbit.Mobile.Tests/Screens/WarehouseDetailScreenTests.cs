using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Core.Inventory;
using Orbit.Contracts.Inventory;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Chat;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// A warehouse item's own details. The phone created every item as one Piece of General with no minimum
/// and no expiry, and offered no way to change any of it - so four fields the browser sets were both
/// invisible and unreachable here.
/// </summary>
public sealed class WarehouseDetailScreenTests
{
    [Fact]
    public async Task An_items_kind_and_minimum_are_shown()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, 5, nameof(InventoryUnit.Piece), null, "None"));

        var screen = await context.OpenAsync(warehouse.LocalId);

        var row = Assert.Single(screen.Items);
        Assert.Contains("Bag", row.Detail);
        Assert.Contains("Kitchen", row.Detail);
        Assert.Contains("5", row.Detail);
    }

    /// <summary>The whole reason to set a minimum, and the same test Orbit.Web's editor makes.</summary>
    [Fact]
    public async Task An_item_below_its_minimum_is_marked()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, 5, nameof(InventoryUnit.Piece), null, "None"),
            new WarehouseItemDto(Guid.NewGuid(), "Tea", "Bag", "Kitchen", 9, 5, nameof(InventoryUnit.Piece), null, "None"));

        var screen = await context.OpenAsync(warehouse.LocalId);

        Assert.True(screen.Items.Single(row => row.Name == "Coffee").IsRunningLow);
        Assert.False(screen.Items.Single(row => row.Name == "Tea").IsRunningLow);
    }

    /// <summary>An item with no minimum is not running low - it has no line to be under.</summary>
    [Fact]
    public async Task An_item_with_no_minimum_is_never_running_low()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 0, null, nameof(InventoryUnit.Piece), null, "None"));

        var screen = await context.OpenAsync(warehouse.LocalId);

        Assert.False(Assert.Single(screen.Items).IsRunningLow);
    }

    [Fact]
    public async Task Editing_an_item_keeps_what_was_typed()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Coffee", "Piece", "General", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(warehouse.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.ProductType = "Bag";
        screen.BeingEdited.Category = "Kitchen";
        screen.BeingEdited.MinimumQuantity = "5";
        screen.BeingEdited.Expires = true;
        screen.BeingEdited.ExpiryDate = new DateTime(2027, 3, 1);
        await screen.SaveItemCommand.ExecuteAsync(null);

        var row = Assert.Single(screen.Items);
        Assert.Equal("Bag", row.Item.ProductType);
        Assert.Equal("Kitchen", row.Item.Category);
        Assert.Equal(5, row.Item.MinimumQuantity);
        Assert.Equal(new DateTime(2027, 3, 1), row.Item.ExpiryDate!.Value.LocalDateTime.Date);
    }

    /// <summary>
    /// An empty minimum box means no minimum, which is not a minimum of zero: zero would mark everything
    /// as adequately stocked forever, and is a different statement about the shelf.
    /// </summary>
    [Fact]
    public async Task Clearing_the_minimum_removes_it_rather_than_setting_zero()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, 5, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(warehouse.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.MinimumQuantity = string.Empty;
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(screen.Items).Item.MinimumQuantity);
    }

    [Fact]
    public async Task Turning_the_expiry_off_removes_the_date()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(
                Guid.NewGuid(), "Milk", "Bottle", "Kitchen", 1, null, nameof(InventoryUnit.Piece),
                new DateTimeOffset(2027, 3, 1, 0, 0, 0, TimeSpan.Zero), "Push"));
        var screen = await context.OpenAsync(warehouse.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.Expires = false;
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Null(Assert.Single(screen.Items).Item.ExpiryDate);
    }

    /// <summary>Cancelling leaves the shelf as it was - the editor is a draft until it is saved.</summary>
    [Fact]
    public async Task Cancelling_changes_nothing()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Coffee", "Piece", "General", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(warehouse.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.ProductType = "Bag";
        screen.CancelItemEditCommand.Execute(null);

        Assert.Null(screen.BeingEdited);
        Assert.Equal("Piece", Assert.Single(screen.Items).Item.ProductType);
    }

    /// <summary>
    /// Orbit.Web has had both since the beginning; the phone showed a warehouse's name without letting
    /// anybody change it, and offered no way to get rid of the warehouse from anywhere at all.
    /// </summary>
    [Fact]
    public async Task A_warehouse_can_be_renamed()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync("Kitchn");
        var screen = await context.OpenAsync(warehouse.LocalId);

        screen.Name = "Kitchen";
        await screen.RenameCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(warehouse.LocalId);
        Assert.Equal("Kitchen", reopened.Name);
    }

    [Fact]
    public async Task A_warehouse_can_be_deleted_and_the_screen_leaves()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync("Kitchen");
        var screen = await context.OpenAsync(warehouse.LocalId);

        await screen.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(await context.StoredWarehousesAsync());
        Assert.Contains(nameof(IScreenNavigator.ShowInventory), context.Navigator.Destinations);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-27T10:00:00Z"));
        private readonly LocalWarehouseRepository _warehouses;
        private readonly WarehouseSynchronizer _synchronizer;

        public ScreenContext()
        {
            Server = new FakeInventoryServer(_clock);
            _warehouses = new LocalWarehouseRepository(_localStore, _clock, FixedNetworkStatus.Online);
            _synchronizer = new WarehouseSynchronizer(
                _localStore, new InventoryClient(Server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<WarehouseSynchronizer>.Instance);
        }

        public FakeInventoryServer Server { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>A warehouse is created empty, so its items are put in by the same update a screen makes.</summary>
        public Task<LocalWarehouse> AddWarehouseAsync(string name)
            => _warehouses.CreateAsync(name);

        /// <summary>What the local store holds now - for the one test that expects it to hold nothing.</summary>
        public async Task<IReadOnlyList<LocalWarehouse>> StoredWarehousesAsync()
            => await _warehouses.GetAllAsync();

        public async Task<LocalWarehouse> AddWarehouseAsync(params WarehouseItemDto[] items)
        {
            var warehouse = await _warehouses.CreateAsync("Kitchen");
            await _warehouses.UpdateAsync(warehouse.LocalId, warehouse.Name, items);
            return warehouse;
        }

        public async Task<WarehouseDetailViewModel> OpenAsync(Guid localId)
        {
            var screen = new WarehouseDetailViewModel(
                _warehouses, _synchronizer, new Translations(new InMemoryLanguageStore()),
                ShareTestPanel.For(_localStore, new ChatRepository(_localStore, _clock)),
                Navigator,
                new InventoryClient(Server.ToHttpClient()), NothingIsBeingEdited(_clock));

            screen.Open(localId);
            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        /// <summary>
        /// A lock over a fake server that answers every claim with "yours" - these tests are about the
        /// editor, and EditLockTests covers what happens when somebody else is in it.
        /// </summary>
        private static EditLock NothingIsBeingEdited(TimeProvider clock)
            => new(FixedNetworkStatus.Online, clock, new Translations(new InMemoryLanguageStore()));

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}

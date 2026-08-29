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
    /// An amount of "2" says nothing about whether that is two bottles or two litres, which is why
    /// Orbit.Web writes the unit beside it. Pieces are left off: "2" of a thing already means two of
    /// them.
    /// </summary>
    [Fact]
    public async Task An_amount_is_said_in_what_it_is_counted_in()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Milk", "Bottle", "Kitchen", 2, 5, nameof(InventoryUnit.Litre), null, "None"),
            new WarehouseItemDto(Guid.NewGuid(), "Cups", "Piece", "Kitchen", 4, null, nameof(InventoryUnit.Piece), null, "None"));

        var screen = await context.OpenAsync(warehouse.LocalId);

        var milk = screen.Items.Single(row => row.Name == "Milk");
        Assert.Equal("2 l", milk.Amount);
        Assert.Contains("5 l", milk.Detail);
        Assert.Equal("4", screen.Items.Single(row => row.Name == "Cups").Amount);
    }

    /// <summary>
    /// A full warehouse could only be read top to bottom on the phone. Narrowing shows fewer rows and
    /// changes nothing about what is stored - the save carries the whole list either way.
    /// </summary>
    [Fact]
    public async Task A_shelf_can_be_narrowed_to_one_kind_of_thing()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"),
            new WarehouseItemDto(Guid.NewGuid(), "Soap", "Bar", "Bathroom", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(warehouse.LocalId);

        screen.ChosenProductType = "Bag";

        Assert.Equal("Coffee", Assert.Single(screen.Items).Name);
        Assert.True(screen.IsNarrowed);
        Assert.Contains("1", screen.FilterNote);
    }

    /// <summary>
    /// The whole shelf comes back, which is what stops somebody saving a warehouse in the belief that
    /// the rows they cannot see are gone.
    /// </summary>
    [Fact]
    public async Task Narrowing_hides_rows_without_dropping_them()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"),
            new WarehouseItemDto(Guid.NewGuid(), "Soap", "Bar", "Bathroom", 1, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(warehouse.LocalId);
        screen.ChosenCategory = "Kitchen";

        await screen.RenameCommand.ExecuteAsync(null);
        screen.ShowEverythingCommand.Execute(null);

        Assert.Equal(2, screen.Items.Count);
        Assert.False(screen.IsNarrowed);
    }

    /// <summary>
    /// A new row is filed under nothing yet, so a filter left in place would hide it the moment it
    /// appeared - which reads as an Add button that does nothing.
    /// </summary>
    [Fact]
    public async Task Adding_an_item_steps_the_filter_aside()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(warehouse.LocalId);
        screen.ChosenProductType = "Bag";

        screen.NewItemName = "Tea";
        await screen.AddItemCommand.ExecuteAsync(null);

        Assert.False(screen.IsNarrowed);
        Assert.Contains(screen.Items, row => row.Name == "Tea");
    }

    /// <summary>
    /// What a MAUI Picker does that no test did: emptying its ItemsSource clears its selection, and the
    /// binding writes that null back here. Every reload empties both pickers before refilling them, so
    /// the null arrived on the way to redrawing the shelf - and the filter, holding it, dereferenced it.
    /// Adding one item to a warehouse was enough to kill the app.
    /// </summary>
    [Fact]
    public async Task A_picker_that_clears_its_own_selection_does_not_take_the_screen_down()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Coffee", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(warehouse.LocalId);

        screen.ChosenCategory = null;
        screen.NewItemName = "Tea";
        await screen.AddItemCommand.ExecuteAsync(null);

        // Nothing narrowed by a selection nobody made: the whole shelf, the new row included.
        Assert.False(screen.IsNarrowed);
        Assert.Equal(2, screen.Items.Count);
    }

    /// <summary>
    /// A shelf is arranged, and the phone could only add to the end of one. Orbit.Web drags rows into
    /// place; the order a warehouse is saved in is the order it is stored in, so the two arrive at the
    /// same shelf.
    /// </summary>
    [Fact]
    public async Task A_product_can_be_moved_up_the_shelf()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            Product("Coffee"), Product("Soap"), Product("Rice"));
        var screen = await context.OpenAsync(warehouse.LocalId);

        await screen.MoveItemUpCommand.ExecuteAsync(screen.Items.Single(row => row.Name == "Rice"));

        Assert.Equal(["Coffee", "Rice", "Soap"], screen.Items.Select(row => row.Name));
    }

    [Fact]
    public async Task A_product_can_be_moved_down_the_shelf()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            Product("Coffee"), Product("Soap"), Product("Rice"));
        var screen = await context.OpenAsync(warehouse.LocalId);

        await screen.MoveItemDownCommand.ExecuteAsync(screen.Items.Single(row => row.Name == "Coffee"));

        Assert.Equal(["Soap", "Coffee", "Rice"], screen.Items.Select(row => row.Name));
    }

    /// <summary>
    /// One place among what is shown, not among what is stored. A narrowed shelf hides rows, and
    /// swapping with a hidden neighbour would move the product without anything on screen changing -
    /// which reads as a button that does nothing.
    /// </summary>
    [Fact]
    public async Task Moving_a_product_on_a_narrowed_shelf_moves_it_past_what_is_shown()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            Product("Coffee", category: "Kitchen"),
            Product("Soap", category: "Bathroom"),
            Product("Rice", category: "Kitchen"));
        var screen = await context.OpenAsync(warehouse.LocalId);
        screen.ChosenCategory = "Kitchen";

        await screen.MoveItemUpCommand.ExecuteAsync(screen.Items.Single(row => row.Name == "Rice"));

        // Visibly one place up among the kitchen rows...
        Assert.Equal(["Rice", "Coffee"], screen.Items.Select(row => row.Name));

        // ...and the hidden row stayed where it was rather than being carried along.
        screen.ShowEverythingCommand.Execute(null);
        Assert.Equal(["Rice", "Coffee", "Soap"], screen.Items.Select(row => row.Name));
    }

    /// <summary>The ends are where a shelf stops, not a failure - the top row has nowhere above it.</summary>
    [Fact]
    public async Task The_ends_of_the_shelf_hold()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(Product("Coffee"), Product("Soap"));
        var screen = await context.OpenAsync(warehouse.LocalId);

        await screen.MoveItemUpCommand.ExecuteAsync(screen.Items[0]);
        await screen.MoveItemDownCommand.ExecuteAsync(screen.Items[1]);

        Assert.Equal(["Coffee", "Soap"], screen.Items.Select(row => row.Name));
    }

    private static WarehouseItemDto Product(string name, string category = "General")
        => new(Guid.NewGuid(), name, "Bag", category, 1, null, nameof(InventoryUnit.Piece), null, "None");

    /// <summary>
    /// The unit is the phone's to set, not just to pass along - a shelf counted in kilograms is stocked
    /// from the phone as often as from the browser.
    /// </summary>
    [Fact]
    public async Task An_items_unit_can_be_changed()
    {
        using var context = new ScreenContext();
        var warehouse = await context.AddWarehouseAsync(
            new WarehouseItemDto(Guid.NewGuid(), "Flour", "Bag", "Kitchen", 2, null, nameof(InventoryUnit.Piece), null, "None"));
        var screen = await context.OpenAsync(warehouse.LocalId);

        screen.EditItemCommand.Execute(screen.Items[0]);
        screen.BeingEdited!.ChosenUnit = InventoryUnitChoice.For(
            screen.BeingEdited.Units, nameof(InventoryUnit.Kilogram));
        await screen.SaveItemCommand.ExecuteAsync(null);

        Assert.Equal(nameof(InventoryUnit.Kilogram), Assert.Single(screen.Items).Item.Unit);
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

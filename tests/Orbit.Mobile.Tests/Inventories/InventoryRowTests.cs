using Orbit.Contracts.Inventories;
using Orbit.Core.Inventories;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Inventory;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Inventories;

/// <summary>
/// What an inventory's row on the list says about itself without being opened. The reason for opening
/// an inventory is usually to find out whether anything on it has run out, and the list said nothing
/// about that at all - so every one of them had to be opened to answer it.
/// </summary>
public sealed class InventoryRowTests
{
    [Fact]
    public void A_row_says_how_many_of_its_things_are_below_their_minimum()
    {
        var row = Describe(Inventory(
            Item("Flour", quantity: 0, minimum: 2),
            Item("Rice", quantity: 5, minimum: 2),
            Item("Olive oil", quantity: 1, minimum: 3)));

        Assert.True(row.HasRunningLow);
        Assert.Equal("2 low", row.RunningLow);
    }

    /// <summary>A shelf that is stocked has nothing to say about it, so the chip is left off rather
    /// than drawn as a zero - the same rule the dashboard's chat counter follows.</summary>
    [Fact]
    public void A_stocked_shelf_says_nothing()
    {
        var row = Describe(Inventory(
            Item("Rice", quantity: 5, minimum: 2), Item("Sugar", quantity: 1)));

        Assert.False(row.HasRunningLow);
        Assert.Equal(string.Empty, row.RunningLow);
    }

    /// <summary>
    /// An item nobody set a minimum for is never low, whatever is left of it: how little is too little
    /// is a judgement, and the shelf has not been given one.
    /// </summary>
    [Fact]
    public void An_item_with_no_minimum_is_never_counted()
    {
        var row = Describe(Inventory(Item("Sugar", quantity: 0)));

        Assert.False(row.HasRunningLow);
    }

    /// <summary>
    /// What a private inventory holds is exactly what being private keeps back, and how much of it has
    /// run out is part of that - see PrivateInventoryTests for the rest of the promise.
    /// </summary>
    [Fact]
    public void A_locked_private_inventory_says_nothing_about_what_it_is_short_of()
    {
        var inventory = Inventory(Item("Flour", quantity: 0, minimum: 2));
        inventory.IsPrivate = true;

        var row = Describe(inventory, privateItemsAreUnlocked: false);

        Assert.False(row.HasRunningLow);
    }

    private static LocalInventory Inventory(params InventoryItemRequest[] items)
        => new() { Name = "Pantry", Items = items };

    private static InventoryItemRequest Item(string name, decimal quantity = 1, decimal? minimum = null)
        => new(
            Guid.NewGuid(), name, string.Empty, string.Empty, quantity, minimum,
            nameof(InventoryUnit.Piece), null, "None");

    /// <summary>
    /// The level a product is kept at is never below what the reader's task lists ask for - the same rule
    /// the server keeps (InventoryItem.EffectiveMinimum). Reading the typed minimum alone, the phone called
    /// a product fine while the server was already restocking it.
    /// </summary>
    [Fact]
    public void A_product_the_lists_ask_more_of_than_its_minimum_is_running_low()
    {
        var flour = new InventoryItemRequest(
            Guid.NewGuid(), "Flour", "Bag", "Kitchen", 3, 2, nameof(InventoryUnit.Piece), null, "None");
        var translations = new Translations(new InMemoryLanguageStore());

        Assert.False(InventoryItemRow.From(flour, translations).IsRunningLow);

        var asTheListsAskForIt = InventoryItemRow.From(flour, translations, usage: 5);

        Assert.True(asTheListsAskForIt.IsRunningLow);
        Assert.Contains("5", asTheListsAskForIt.Detail);
    }

    /// <summary>
    /// A group shelf names what it gathers, so the list says which shelves are read together without
    /// anything being opened - see Orbit.Core.Inventories.Inventory.GathersInventoryIds. 2026-09-18.
    /// </summary>
    [Fact]
    public void A_group_names_the_shelves_it_gathers()
    {
        var fridge = AShelf("Fridge");
        var pantry = AShelf("Pantry");
        var kitchen = new LocalInventory
        {
            Name = "Kitchen",
            GathersServerIds = [fridge.ServerId!.Value, pantry.ServerId!.Value]
        };

        var row = Describe(kitchen, everyShelf: [kitchen, fridge, pantry]);

        Assert.True(row.IsGroup);
        Assert.True(row.HasGathers);
        Assert.Equal("Holds: Fridge, Pantry", row.Gathers);
    }

    /// <summary>
    /// A member this phone has not got - not synced yet, or deleted - is passed over rather than named
    /// as a shelf that cannot be opened, the same way a link to a list nobody has reads as nothing there.
    /// </summary>
    [Fact]
    public void A_member_this_phone_has_not_got_is_passed_over()
    {
        var fridge = AShelf("Fridge");
        var kitchen = new LocalInventory
        {
            Name = "Kitchen",
            GathersServerIds = [fridge.ServerId!.Value, Guid.NewGuid()]
        };

        Assert.Equal("Holds: Fridge", Describe(kitchen, everyShelf: [kitchen, fridge]).Gathers);
    }

    [Fact]
    public void An_ordinary_shelf_gathers_nothing_and_says_nothing()
    {
        var row = Describe(Inventory(Item("Rice")));

        Assert.False(row.IsGroup);
        Assert.Equal(string.Empty, row.Gathers);
    }

    private static LocalInventory AShelf(string name)
        => new() { LocalId = Guid.NewGuid(), ServerId = Guid.NewGuid(), Name = name };

    private static InventoryRow Describe(
        LocalInventory inventory, bool privateItemsAreUnlocked = true,
        IReadOnlyList<LocalInventory>? everyShelf = null)

        => InventoryRow.From(
            inventory, hasUnsentChanges: false, FixedNetworkStatus.Online,
            new Translations(new InMemoryLanguageStore()), privateItemsAreUnlocked,
            everyShelf: everyShelf);
}

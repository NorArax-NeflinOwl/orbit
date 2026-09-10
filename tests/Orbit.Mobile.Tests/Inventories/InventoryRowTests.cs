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

    private static InventoryRow Describe(LocalInventory inventory, bool privateItemsAreUnlocked = true)
        => InventoryRow.From(
            inventory, hasUnsentChanges: false, FixedNetworkStatus.Online,
            new Translations(new InMemoryLanguageStore()), privateItemsAreUnlocked);
}

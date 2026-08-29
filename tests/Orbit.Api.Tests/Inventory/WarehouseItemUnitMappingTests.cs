using Orbit.Api.Inventory;
using Orbit.Contracts.Inventory;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

/// <summary>
/// What an incoming warehouse row says its amounts are counted in. The rule the whole feature is
/// written to is that saying nothing means pieces - the same thing every row already on a shelf was
/// given when the column was added - so a save that omits it has to be read that way rather than
/// refused.
/// </summary>
public sealed class WarehouseItemUnitMappingTests
{
    [Fact]
    public void A_row_that_names_its_unit_is_taken_at_its_word()
    {
        Assert.Equal(InventoryUnit.Kilogram, InventoryEndpoints.UnitOf(Item("Kilogram")));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void A_row_that_says_nothing_is_counted_in_pieces(string? unit)
    {
        // A client built before units existed - a cached copy of the app, say - sends exactly this.
        // Refusing it left such a client unable to save a warehouse at all, with a message about a
        // field its version has never heard of.
        Assert.Equal(InventoryUnit.Piece, InventoryEndpoints.UnitOf(Item(unit)));
    }

    [Fact]
    public void A_unit_that_is_named_but_not_recognised_is_still_refused()
    {
        // A typo is not a silence, and guessing at one would store an amount in something nobody meant.
        var refusal = Assert.Throws<InvalidRequestException>(() => InventoryEndpoints.UnitOf(Item("Furlong")));

        Assert.Contains("unit", refusal.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(nameof(InventoryUnit.Kilogram), refusal.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_whole_list_comes_through_with_each_row_reading_its_own_unit()
    {
        var inputs = InventoryEndpoints.ToDomainItems([Item("Litre"), Item(null), Item("Pack")]);

        Assert.Equal(
            [InventoryUnit.Litre, InventoryUnit.Piece, InventoryUnit.Pack],
            inputs.Select(input => input.Unit));
    }

    private static WarehouseItemDto Item(string? unit)
        => new(
            Id: null, "Flour", "Food", "Dry", Quantity: 2, MinimumQuantity: 1, unit!,
            ExpiryDate: null, ExpiryNotificationChannel: "None");
}

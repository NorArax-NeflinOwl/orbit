using Orbit.Core.Inventories;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The units the item editor offers. Built from the enum rather than listed twice, which is what these
/// hold in place: a unit added to <see cref="InventoryUnit"/> has to reach the picker without anybody
/// remembering to add it there as well, and its short form has to be the same one the server writes
/// into a restock errand.
/// </summary>
public sealed class InventoryUnitOptionTests
{
    [Fact]
    public void Every_unit_there_is_can_be_picked()
    {
        Assert.Equal(
            Enum.GetValues<InventoryUnit>().Select(unit => unit.ToString()),
            InventoryUnitOption.All.Select(option => option.Value));
    }

    [Fact]
    public void Each_one_is_written_short_the_same_way_the_server_writes_it()
    {
        // The server puts this into "Restock: Flour (5 kg)" and the client reads it back to say it in
        // the reader's language - two lists would drift and leave a unit nobody translated.
        foreach (var option in InventoryUnitOption.All)
        {
            var unit = Enum.Parse<InventoryUnit>(option.Value);

            Assert.Equal(InventoryUnitShortForm.Of(unit), option.ShortName);
        }
    }

    [Fact]
    public void Pieces_are_what_an_item_that_says_nothing_gets()
    {
        Assert.Equal(nameof(InventoryUnit.Piece), InventoryUnitOption.Default.Value);
    }

    [Theory]
    [InlineData("Kilogram", "Kilogram")]
    [InlineData("kilogram", "Kilogram")]
    [InlineData("Furlong", "Piece")]
    [InlineData("", "Piece")]
    [InlineData(null, "Piece")]
    public void An_unreadable_unit_reads_as_pieces_rather_than_as_nothing(string? stored, string expected)
    {
        // Null is not hypothetical: a private inventory sealed before units existed carries no unit at
        // all, and without this the picker showed pieces while the row held nothing.
        Assert.Equal(expected, InventoryUnitOption.For(stored).Value);
    }
}

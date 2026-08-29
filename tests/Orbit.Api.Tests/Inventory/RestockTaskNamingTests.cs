using Orbit.Core.Inventory;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

/// <summary>
/// What the restock list and its entries are called. Two paths write these - a warehouse item going low
/// and a task list coming up short - so reading one back has to find the same product either way.
/// </summary>
public sealed class RestockTaskNamingTests
{
    [Fact]
    public void The_list_says_which_warehouse_it_restocks()
    {
        // Three warehouses used to mean three lists called the same thing.
        Assert.Equal("Restock supplies - Pantry", RestockTaskNaming.TitleFor("Pantry"));
    }

    [Fact]
    public void A_warehouse_with_no_name_leaves_the_title_as_it_was()
    {
        Assert.Equal("Restock supplies", RestockTaskNaming.TitleFor("   "));
    }

    [Fact]
    public void An_entry_carries_how_many_to_bring_back()
    {
        Assert.Equal("Restock: Flour (5)", RestockTaskNaming.EntryFor("Flour", 5, InventoryUnit.Piece));
    }

    [Fact]
    public void A_whole_number_reads_as_one()
    {
        Assert.Equal("Restock: Flour (5)", RestockTaskNaming.EntryFor("Flour", 5.00m, InventoryUnit.Piece));
        Assert.Equal("Restock: Flour (1.5)", RestockTaskNaming.EntryFor("Flour", 1.5m, InventoryUnit.Piece));
    }

    [Fact]
    public void An_entry_for_something_with_no_number_says_only_what_it_is()
    {
        Assert.Equal("Restock: Flour", RestockTaskNaming.EntryFor("Flour", null, InventoryUnit.Piece));
        Assert.Equal("Restock: Flour", RestockTaskNaming.EntryFor("Flour", 0, InventoryUnit.Piece));
    }


    [Fact]
    public void An_entry_says_what_the_number_is_counted_in()
    {
        // "5" of something measured in kilograms does not say enough to act on.
        Assert.Equal("Restock: Flour (5 kg)", RestockTaskNaming.EntryFor("Flour", 5, InventoryUnit.Kilogram));
        Assert.Equal("Restock: Milk (1.5 l)", RestockTaskNaming.EntryFor("Milk", 1.5m, InventoryUnit.Litre));
    }

    [Fact]
    public void Pieces_are_left_off_because_a_bare_number_already_means_them()
    {
        Assert.Equal("Restock: Screw (5)", RestockTaskNaming.EntryFor("Screw", 5, InventoryUnit.Piece));
    }

    [Fact]
    public void An_errand_counted_off_a_checklist_carries_no_unit_at_all()
    {
        // Repetition is the quantity there (see StockRequirementCounter), so the number counts lines
        // rather than an amount of anything measurable.
        Assert.Equal("Restock: Screw (3)", RestockTaskNaming.EntryFor("Screw", 3, unit: null));
    }

    [Fact]
    public void The_product_is_still_read_back_from_an_entry_carrying_a_unit()
    {
        // This is what keeps an errand for five kilos and one for eight the same errand.
        Assert.Equal("Flour", RestockTaskNaming.ProductIn("Restock: Flour (5 kg)"));
    }
    [Theory]
    [InlineData("Restock: Flour (5)", "Flour")]
    [InlineData("Restock: Flour", "Flour")]
    [InlineData("Restock: Olive oil (1.5)", "Olive oil")]
    [InlineData("Flour (5)", "Flour")]
    public void The_product_is_read_back_whatever_number_the_entry_carries(string description, string expected)
    {
        // This is what makes an errand for five and one for eight the same errand rather than two.
        Assert.Equal(expected, RestockTaskNaming.ProductIn(description));
    }

    [Fact]
    public void A_product_whose_own_name_ends_in_brackets_survives()
    {
        Assert.Equal("Flour (organic)", RestockTaskNaming.ProductIn("Restock: Flour (organic) (5)"));
    }

    [Fact]
    public void Something_that_is_not_a_restock_entry_is_not_taken_for_one()
    {
        Assert.False(RestockTaskNaming.IsRestockEntry("Update stock levels"));
        Assert.True(RestockTaskNaming.IsRestockEntry("Restock: Flour (5)"));
    }
}

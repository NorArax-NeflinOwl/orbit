namespace Orbit.Core.Inventory;

/// <summary>
/// What an item's amount is counted in. A fixed list rather than free text, unlike the product type and
/// the category beside it: the amount and the minimum are compared as bare numbers (see
/// <see cref="InventoryItem.IsBelowMinimum"/>), so the two have to be in the same unit, and "szt." typed
/// three different ways would leave a shelf that looks stocked and a restock task nobody understands.
/// </summary>
public enum InventoryUnit
{
    /// <summary>Counted one at a time - what most stock is, and what an item says nothing about it gets.</summary>
    Piece,
    Kilogram,
    Milligram,
    Litre,
    Millilitre,

    /// <summary>Whatever the shop sells it in - a box of tea bags is one of these, not forty pieces.</summary>
    Pack
}

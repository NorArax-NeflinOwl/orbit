namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// One thing found by the search across every inventory, and which inventory it is on. Wraps the row
/// the inventory screen already builds rather than restating it, so what a found item says about itself
/// - how much there is, in what it is counted - is written once.
/// </summary>
/// <param name="InventoryName">
/// Where it is, which is the point of the whole search: the name only says what was found.
/// </param>
public sealed record InventoryItemMatch(Guid InventoryLocalId, string InventoryName, InventoryItemRow Item)
{
    public string Name => Item.Name;

    public string Amount => Item.Amount;
}

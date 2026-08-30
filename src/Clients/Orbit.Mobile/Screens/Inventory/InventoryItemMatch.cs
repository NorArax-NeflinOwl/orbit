namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// One thing found by the search across every warehouse, and which warehouse it is on. Wraps the row
/// the warehouse screen already builds rather than restating it, so what a found item says about itself
/// - how much there is, in what it is counted - is written once.
/// </summary>
/// <param name="WarehouseName">
/// Where it is, which is the point of the whole search: the name only says what was found.
/// </param>
public sealed record InventoryItemMatch(Guid WarehouseLocalId, string WarehouseName, WarehouseItemRow Item)
{
    public string Name => Item.Name;

    public string Amount => Item.Amount;
}

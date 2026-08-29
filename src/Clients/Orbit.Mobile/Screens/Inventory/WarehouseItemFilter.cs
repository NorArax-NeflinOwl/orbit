using Orbit.Contracts.Inventory;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// What the reader has narrowed a shelf down to. Narrows what is on screen, never what is saved: a save
/// carries the whole item list, and anything missing from it is deleted, so a filter that reached the
/// save would delete exactly the rows it hid. The same split Orbit.Web's editor makes.
/// </summary>
public sealed class WarehouseItemFilter
{
    /// <summary>Empty means "any", which is what an untouched filter says.</summary>
    public string ProductType { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public bool IsActive => ProductType.Length > 0 || Category.Length > 0;

    public void Clear()
    {
        ProductType = string.Empty;
        Category = string.Empty;
    }

    public bool Matches(WarehouseItemDto item)
        => Matches(ProductType, item.ProductType) && Matches(Category, item.Category);

    private static bool Matches(string chosen, string itemValue)
        => chosen.Length == 0 || string.Equals(chosen, itemValue.Trim(), StringComparison.CurrentCultureIgnoreCase);
}

using Orbit.Contracts.Inventories;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// What the reader has narrowed a shelf down to. Narrows what is on screen, never what is saved: a save
/// carries the whole item list, and anything missing from it is deleted, so a filter that reached the
/// save would delete exactly the rows it hid. The same split Orbit.Web's editor makes.
/// </summary>
public sealed class InventoryItemFilter
{
    /// <summary>Empty means "any", which is what an untouched filter says.</summary>
    public string ProductType { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// What the reader is looking for by name. Matched anywhere in the name rather than from the start:
    /// a shelf holds "Flour, wheat" and "Wholemeal flour", and somebody typing "flour" means both. The
    /// type and the category are picked from what is there, so they match exactly; this one is typed,
    /// so it cannot.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    public bool IsActive => ProductType.Length > 0 || Category.Length > 0 || Name.Trim().Length > 0;

    public void Clear()
    {
        ProductType = string.Empty;
        Category = string.Empty;
        Name = string.Empty;
    }

    public bool Matches(InventoryItemRequest item)
        => Matches(ProductType, item.ProductType)
            // One of them is enough: an item filed under both "baking" and "dry goods" is what either
            // filter is looking for.
            && (Category.Length == 0 || item.AllCategories.Any(
                category => string.Equals(Category, category.Trim(), StringComparison.CurrentCultureIgnoreCase)))
            && Contains(Name, item.Name);

    private static bool Matches(string chosen, string itemValue)
        => chosen.Length == 0 || string.Equals(chosen, itemValue.Trim(), StringComparison.CurrentCultureIgnoreCase);

    private static bool Contains(string typed, string itemName)
        => typed.Trim() is not { Length: > 0 } wanted
            || itemName.Contains(wanted, StringComparison.CurrentCultureIgnoreCase);
}

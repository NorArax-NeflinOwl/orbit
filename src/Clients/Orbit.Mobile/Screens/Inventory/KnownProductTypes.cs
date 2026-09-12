using Orbit.Mobile.Data;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// What this account already calls kinds of product, for the product-type box on a product's form - see
/// InventoryItemEditor.OfferedProductTypes. Every shelf's answers and every task entry's own: the same two
/// halves Orbit.Web's editor offers, since an entry describing something no shelf has yet carries its type
/// itself. Read from this phone's own copies, so the offer works offline.
///
/// One rule for both screens that open that form - a task list's and an inventory's - so the same box
/// cannot offer one thing on one screen and something else on the other.
/// </summary>
public static class KnownProductTypes
{
    public static IReadOnlyList<string> From(IEnumerable<LocalInventory> shelves, IEnumerable<LocalTaskList> lists)
        => [.. shelves
            .SelectMany(inventory => inventory.Items.Select(product => product.ProductType))
            .Concat(lists.SelectMany(list => list.Items).Select(item => item.Product?.ProductType ?? string.Empty))
            .Select(productType => productType.Trim())
            .Where(productType => productType.Length > 0)
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(productType => productType, StringComparer.CurrentCultureIgnoreCase)];
}

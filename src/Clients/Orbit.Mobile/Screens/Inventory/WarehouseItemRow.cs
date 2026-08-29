using Orbit.Contracts.Inventory;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// One item on a warehouse's list. Carries what the shelf actually says about it - what kind of thing
/// it is, how many there are, whether that is below the minimum, and when it goes off - because the
/// phone showed only a name and a number, and every other field was invisible whatever the browser set.
/// </summary>
/// <param name="Detail">Already in the reader's language, so the row itself needs no dictionary.</param>
/// <param name="Amount">
/// How many there are, said in what they are counted in - "2 l", not "2". Pieces are left off, since
/// "2" of a thing already means two of them; the same rule the server follows when it names a restock
/// errand (see RestockTaskNaming).
/// </param>
public sealed record WarehouseItemRow(
    WarehouseItemDto Item, string Detail, string Amount, bool IsRunningLow, string Expiry)
{
    public static WarehouseItemRow From(WarehouseItemDto item, Translations translations)
        => new(
            item,
            Describe(item, translations),
            Measure(item.Quantity, item.Unit, translations),
            // The same test Orbit.Web's editor makes: a minimum that is set and not met.
            item.MinimumQuantity is { } minimum && item.Quantity < minimum,
            item.ExpiryDate is { } expiry
                ? translations.Format("Expires {0}", expiry.LocalDateTime.ToString("d", translations.DisplayCulture))
                : string.Empty);

    public string Name => Item.Name;

    public bool HasExpiry => Expiry.Length > 0;

    private static string Describe(WarehouseItemDto item, Translations translations)
    {
        var kind = string.Join(" · ", new[] { item.ProductType, item.Category }.Where(part => part.Length > 0));

        return item.MinimumQuantity is { } minimum
            ? $"{kind} · {translations.Format("Minimum: {0}", Measure(minimum, item.Unit, translations))}"
            : kind;
    }

    private static string Measure(decimal amount, string unit, Translations translations)
        => InventoryUnitChoice.ShortFormOf(unit, translations) is { Length: > 0 } shortForm
            ? $"{amount} {shortForm}"
            : amount.ToString(translations.DisplayCulture);
}

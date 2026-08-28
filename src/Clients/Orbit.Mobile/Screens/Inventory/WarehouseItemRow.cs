using Orbit.Contracts.Inventory;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// One item on a warehouse's list. Carries what the shelf actually says about it - what kind of thing
/// it is, how many there are, whether that is below the minimum, and when it goes off - because the
/// phone showed only a name and a number, and every other field was invisible whatever the browser set.
/// </summary>
/// <param name="Detail">Already in the reader's language, so the row itself needs no dictionary.</param>
public sealed record WarehouseItemRow(WarehouseItemDto Item, string Detail, bool IsRunningLow, string Expiry)
{
    public static WarehouseItemRow From(WarehouseItemDto item, Translations translations)
        => new(
            item,
            Describe(item, translations),
            // The same test Orbit.Web's editor makes: a minimum that is set and not met.
            item.MinimumQuantity is { } minimum && item.Quantity < minimum,
            item.ExpiryDate is { } expiry
                ? translations.Format("Expires {0}", expiry.LocalDateTime.ToString("d", translations.DisplayCulture))
                : string.Empty);

    public string Name => Item.Name;

    public decimal Quantity => Item.Quantity;

    public bool HasExpiry => Expiry.Length > 0;

    private static string Describe(WarehouseItemDto item, Translations translations)
    {
        var kind = string.Join(" · ", new[] { item.ProductType, item.Category }.Where(part => part.Length > 0));

        return item.MinimumQuantity is { } minimum
            ? $"{kind} · {translations.Format("Minimum: {0}", minimum)}"
            : kind;
    }
}

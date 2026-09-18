using Orbit.Contracts.Inventories;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// One item on an inventory's list. Carries what the shelf actually says about it - what kind of thing
/// it is, how many there are, whether that is below the minimum, and when it goes off - because the
/// phone showed only a name and a number, and every other field was invisible whatever the browser set.
/// </summary>
/// <param name="Detail">Already in the reader's language, so the row itself needs no dictionary.</param>
/// <param name="Amount">
/// How many there are, said in what they are counted in - "2 l", not "2". Pieces are left off, since
/// "2" of a thing already means two of them; the same rule the server follows when it names a restock
/// errand (see RestockTaskNaming).
/// </param>
/// <param name="IsPointedAt">
/// Whether this is the row the shelf was opened for - see IScreenNavigator.ShowInventory. Marked rather
/// than lifted to the top: a shelf read in one order should not rearrange itself around where somebody
/// came from.
/// </param>
public sealed record InventoryItemRow(
    InventoryItemRequest Item, string Detail, string Amount, bool IsRunningLow, string Expiry,
    bool IsPointedAt = false)
{
    public static InventoryItemRow From(
        InventoryItemRequest item, Translations translations, Guid? pointedAtProductId = null,
        DateTimeOffset? arrivedAtUtc = null, decimal usage = 0,
        IReadOnlyList<string>? askedForBy = null)
        => new(
            item,
            Describe(item, translations, usage),
            Measure(item.Quantity, item.Unit, translations),
            // The same test Orbit.Web's editor makes, against the level the shelf is actually kept at:
            // never below what the task lists ask for - see KeptAt.
            KeptAt(item, usage) is { } minimum && item.Quantity < minimum,

            item.ExpiryDate is { } expiry
                ? translations.Format("Expires {0}", expiry.LocalDateTime.ToString("d", translations.DisplayCulture))
                : string.Empty,
            IsPointedAt: pointedAtProductId is { } pointedAt && item.Id == pointedAt)
        {
            NameForAReader = pointedAtProductId is { } wanted && item.Id == wanted
                ? translations.Format("{0}, what you were sent here for", item.Name)
                : item.Name,
            Arrived = arrivedAtUtc is { } arrived
                ? translations.Format(
                    "added {0}", arrived.LocalDateTime.ToString("d", translations.DisplayCulture))
                : string.Empty,
            AskedFor = askedForBy is { Count: > 0 } asking
                ? translations.Format("asked for by {0}", string.Join(", ", asking))
                : string.Empty
        };

    public string Name => Item.Name;

    /// <summary>
    /// What a screen reader says for this row. The mark on a pointed-at row is a colour and a bar, and
    /// a colour is nothing to somebody who cannot see it - so the row says so in words as well.
    /// </summary>
    public string NameForAReader { get; private init; } = string.Empty;

    public bool HasExpiry => Expiry.Length > 0;

    /// <summary>
    /// When this batch arrived, in the reader's language, or nothing for one no server has accepted yet.
    /// A row is a batch rather than a product - two rows of one name are two deliveries of it - and this
    /// is what tells them apart. Orbit.Web's shelf says it in the same words.
    /// </summary>
    public string Arrived { get; private init; } = string.Empty;

    public bool HasArrived => Arrived.Length > 0;

    /// <summary>
    /// Which lists ask for this row, named and said once each - see Orbit.Core.Inventories.ShelfDemand,
    /// and Orbit.Web's shelf, which says the same thing beside the same field. The number alone was
    /// here before, in <see cref="Detail"/>'s minimum: it says a change to this row reaches somebody
    /// without saying whom, and somebody typing a new minimum is deciding for those lists. Empty for a
    /// row nothing asks for, and for a shelf whose demand this phone could not read - see
    /// InventoryDetailViewModel.ReadWhoAsksForTheseAsync, which is best effort on purpose.
    /// </summary>
    public string AskedFor { get; private init; } = string.Empty;

    public bool IsAskedFor => AskedFor.Length > 0;

    private static string Describe(InventoryItemRequest item, Translations translations, decimal usage)
    {
        var kind = string.Join(
            " · ", item.AllCategories.Prepend(item.ProductType).Where(part => part.Length > 0));

        return KeptAt(item, usage) is { } minimum
            ? $"{kind} · {translations.Format("Minimum: {0}", Measure(minimum, item.Unit, translations))}"
            : kind;
    }

    /// <summary>
    /// The level this row is kept at: the minimum somebody set, never lower than what the reader's task
    /// lists ask for - see LocalInventory.ItemUsage and Orbit.Core.Inventories.InventoryItem.EffectiveMinimum,
    /// which is the same rule on the server. Null when neither says anything.
    /// </summary>
    private static decimal? KeptAt(InventoryItemRequest item, decimal usage)
        => usage > 0 ? Math.Max(item.MinimumQuantity ?? 0, usage) : item.MinimumQuantity;


    private static string Measure(decimal amount, string unit, Translations translations)
        => InventoryUnitChoice.ShortFormOf(unit, translations) is { Length: > 0 } shortForm
            ? $"{amount} {shortForm}"
            : amount.ToString(translations.DisplayCulture);
}

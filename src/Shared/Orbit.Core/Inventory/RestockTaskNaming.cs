using System.Globalization;

namespace Orbit.Core.Inventory;

/// <summary>
/// What Orbit calls the list it keeps for a warehouse, and the entries on it. One place, because two
/// paths create restock entries (a warehouse item going low, and a task list coming up short) and both
/// have to agree - an entry created one way has to be recognised as the same errand by the other, or the
/// same product ends up on the list twice.
/// </summary>
public static class RestockTaskNaming
{
    /// <summary>
    /// Public because a client showing one of these names has to recognise it to say it in the reader's
    /// language - see Orbit.Web's OrbitWrittenNames. What is stored stays this English either way: it is
    /// also how <see cref="IsManagedTitle"/> knows its own list again.
    /// </summary>
    public const string ListTitlePrefix = "Restock supplies";

    /// <summary>Public for the same reason as <see cref="ListTitlePrefix"/>.</summary>
    public const string EntryPrefix = "Restock: ";

    /// <summary>
    /// The standing, never-recreated reminder on every restock list. Named here rather than only where
    /// it is created, because the screen showing the list has to know which entry this is: crossing it
    /// off is a claim about the whole shelf, not about one product.
    /// </summary>
    public const string UpdateStockReminderDescription = "Update stock levels";

    /// <summary>
    /// The list's title, which names the warehouse it restocks: an account with three warehouses had
    /// three lists called the same thing, and no way to tell from the tasks page which was which.
    /// </summary>
    public static string TitleFor(string warehouseName)
        => string.IsNullOrWhiteSpace(warehouseName) ? ListTitlePrefix : $"{ListTitlePrefix} - {warehouseName.Trim()}";

    /// <summary>Whether a title is one of ours, so an existing list can be renamed when its warehouse is.</summary>
    public static bool IsManagedTitle(string title) => title.StartsWith(ListTitlePrefix, StringComparison.Ordinal);

    /// <summary>
    /// One entry, carrying how many to bring back: "Restock: Flour (5)". The number is what the shelf is
    /// meant to hold - reading the errand should not need the warehouse open beside it.
    /// </summary>
    public static string EntryFor(string productName, decimal? quantity)
    {
        var name = productName.Trim();
        return quantity is { } wanted && wanted > 0
            ? $"{EntryPrefix}{name} ({Format(wanted)})"
            : $"{EntryPrefix}{name}";
    }

    /// <summary>
    /// The product an entry is about, whatever number it carries. This is what makes "Restock: Flour (5)"
    /// and "Restock: Flour (8)" the same errand rather than two, so a changed minimum does not put a
    /// second copy on the list.
    /// </summary>
    public static string ProductIn(string description)
    {
        var text = description.Trim();
        if (text.StartsWith(EntryPrefix, StringComparison.CurrentCultureIgnoreCase))
        {
            text = text[EntryPrefix.Length..];
        }

        var openingBracket = text.LastIndexOf(" (", StringComparison.Ordinal);
        return (openingBracket < 0 || !text.EndsWith(')') ? text : text[..openingBracket]).Trim();
    }

    /// <summary>Whether a task entry is one of ours at all.</summary>
    public static bool IsRestockEntry(string description)
        => description.TrimStart().StartsWith(EntryPrefix, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>Whole numbers read as whole numbers: "(5)", not "(5.00)".</summary>
    private static string Format(decimal quantity)
        => quantity == decimal.Truncate(quantity)
            ? decimal.Truncate(quantity).ToString(CultureInfo.InvariantCulture)
            : quantity.ToString("0.##", CultureInfo.InvariantCulture);
}

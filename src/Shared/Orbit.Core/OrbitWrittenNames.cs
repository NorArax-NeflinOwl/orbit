using Orbit.Core.Inventory;

namespace Orbit.Core;

/// <summary>
/// The names Orbit writes for itself, said in the reader's language.
///
/// A warehouse's restock list and the errands on it are created on the server (see RestockTaskNaming),
/// which has no idea what language anybody reads in - so an otherwise Polish Tasks page carried one list
/// called "Restock supplies - Spiżarnia" and a row saying "Restock: Mąka (5)".
///
/// What is stored stays English, deliberately: the server recognises its own list again by that name
/// when a warehouse is renamed, and an entry by its prefix when a shortfall is raised twice. The
/// translation happens here instead, on the way to the screen, and never on the way back.
///
/// Shared rather than the browser's own, because the phone shows the same names off the same server and
/// was showing them in English on an otherwise Polish screen. It lives beside RestockTaskNaming, which
/// writes them, and it takes a lookup rather than either client's Translations class - the browser and
/// the phone each have one of their own, and a delegate is the only thing the two have in common.
/// </summary>
public static class OrbitWrittenNames
{
    /// <summary>
    /// <paramref name="name"/> in the reader's language when Orbit wrote it, and unchanged when anybody
    /// else did. The part a person chose - the warehouse's name, the product - rides along untouched.
    /// </summary>
    /// <param name="say">
    /// How this client turns an English string into the reader's language - <c>key => translations[key]</c>
    /// on both. A delegate because the browser and the phone each have a Translations of their own.
    /// </param>
    public static string Translate(Func<string, string> say, string name)
    {
        if (name == RestockTaskNaming.UpdateStockReminderDescription)
        {
            return say(name);
        }

        if (name.StartsWith(RestockTaskNaming.EntryPrefix, StringComparison.Ordinal))
        {
            return say("Restock:") + " " + TranslateUnit(say, name[RestockTaskNaming.EntryPrefix.Length..]);
        }

        if (name.StartsWith(RestockTaskNaming.ListTitlePrefix, StringComparison.Ordinal))
        {
            return say(RestockTaskNaming.ListTitlePrefix) + name[RestockTaskNaming.ListTitlePrefix.Length..];
        }

        return name;
    }

    /// <summary>
    /// The unit at the end of an errand Orbit wrote - "Flour (5 kg)" - said in the reader's language.
    /// Only a trailing "(number unit)" whose unit is one Orbit itself writes is touched, so a product
    /// somebody named "Flour (organic)" comes back exactly as they typed it.
    /// </summary>
    private static string TranslateUnit(Func<string, string> say, string entry)
    {
        var openingBracket = entry.LastIndexOf(" (", StringComparison.Ordinal);
        if (!entry.EndsWith(')') || openingBracket < 0)
        {
            return entry;
        }

        var inBrackets = entry[(openingBracket + 2)..^1];
        var lastSpace = inBrackets.LastIndexOf(' ');
        if (lastSpace < 0)
        {
            return entry;
        }

        var shortForm = inBrackets[(lastSpace + 1)..];
        return InventoryUnitShortForm.Read(shortForm) is null
            ? entry
            : $"{entry[..(openingBracket + 2)]}{inBrackets[..lastSpace]} {say(shortForm)})";
    }
}

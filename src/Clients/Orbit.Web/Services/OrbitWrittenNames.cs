using Orbit.Core.Inventory;

namespace Orbit.Web.Services;

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
/// </summary>
public static class OrbitWrittenNames
{
    /// <summary>
    /// <paramref name="name"/> in the reader's language when Orbit wrote it, and unchanged when anybody
    /// else did. The part a person chose - the warehouse's name, the product - rides along untouched.
    /// </summary>
    public static string Translate(Translations translations, string name)
    {
        if (name == RestockTaskNaming.UpdateStockReminderDescription)
        {
            return translations[name];
        }

        if (name.StartsWith(RestockTaskNaming.EntryPrefix, StringComparison.Ordinal))
        {
            return translations["Restock:"] + " " + name[RestockTaskNaming.EntryPrefix.Length..];
        }

        if (name.StartsWith(RestockTaskNaming.ListTitlePrefix, StringComparison.Ordinal))
        {
            return translations[RestockTaskNaming.ListTitlePrefix] + name[RestockTaskNaming.ListTitlePrefix.Length..];
        }

        return name;
    }
}

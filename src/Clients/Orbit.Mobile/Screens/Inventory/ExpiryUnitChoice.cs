using Orbit.Core.Inventories;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// One entry in the "keeps for" picker - the unit, and the word for it in the reader's language.
/// Shaped like InventoryUnitChoice, which the picker beside it uses.
/// </summary>
public sealed record ExpiryUnitChoice(ExpiryUnit Unit, string Name)
{
    /// <summary>
    /// Every unit, in the order the web's dropdown offers them, starting with the one that means the
    /// item does not expire at all.
    /// </summary>
    public static IReadOnlyList<ExpiryUnitChoice> All(Translations translations)
        => [.. Enum.GetValues<ExpiryUnit>().Select(unit => new ExpiryUnitChoice(unit, Describe(unit, translations)))];

    public static ExpiryUnitChoice For(IReadOnlyList<ExpiryUnitChoice> choices, ExpiryUnit unit)
        => choices.FirstOrDefault(choice => choice.Unit == unit) ?? choices[0];

    private static string Describe(ExpiryUnit unit, Translations translations) => unit switch
    {
        ExpiryUnit.Days => translations["days"],
        ExpiryUnit.Weeks => translations["weeks"],
        ExpiryUnit.Months => translations["months"],
        ExpiryUnit.Years => translations["years"],
        _ => translations["No date"]
    };
}

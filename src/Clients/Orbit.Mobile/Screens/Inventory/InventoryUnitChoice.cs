using Orbit.Core.Inventories;
using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Inventory;

/// <summary>
/// One entry in an item's unit picker - the wire value the API stores, the full name shown while
/// picking, and the short form written beside an amount. "2 kg" is what a shelf label says; "2
/// Kilogram" is not. The phone's counterpart to Orbit.Web's InventoryUnitOption, and the same shape as
/// <see cref="NotificationChannelChoice"/>.
///
/// Built from the enum rather than listed again, so a unit added there turns up in the picker without
/// anybody remembering to add it twice.
/// </summary>
public sealed record InventoryUnitChoice(string Value, string Name, string ShortName)
{
    public static IReadOnlyList<InventoryUnitChoice> All(Translations translations)
        =>
        [
            .. Enum.GetValues<InventoryUnit>()
                .Select(unit => new InventoryUnitChoice(
                    unit.ToString(),
                    translations[unit.ToString()],
                    translations[InventoryUnitShortForm.Of(unit)]))
        ];

    /// <summary>
    /// The one whose wire value this is. An unrecognised value reads as the first - pieces - rather than
    /// as nothing at all: the picker only offers the units above, and a row saved by something else
    /// should still show an amount somebody can read. The same fallback Orbit.Web makes.
    /// </summary>
    public static InventoryUnitChoice For(IReadOnlyList<InventoryUnitChoice> all, string value)
        => all.FirstOrDefault(choice => string.Equals(choice.Value, value, StringComparison.OrdinalIgnoreCase))
            ?? all[0];

    /// <summary>
    /// What to write after an amount, or nothing for pieces - "(5)" of a thing already means five of
    /// them, which is the rule the server follows when it names a restock errand.
    /// </summary>
    public static string ShortFormOf(string value, Translations translations)
        => Enum.TryParse<InventoryUnit>(value, ignoreCase: true, out var unit) && unit is not InventoryUnit.Piece
            ? translations[InventoryUnitShortForm.Of(unit)]
            : string.Empty;
}

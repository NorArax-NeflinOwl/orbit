using Orbit.Core.Inventory;

namespace Orbit.Web.Services;

/// <summary>
/// One selectable entry in an item's unit dropdown - pairs the wire value (matching
/// <see cref="InventoryUnit"/> on the API side) with the full name shown while picking and the short
/// form written beside an amount. "2 kg" is what a shelf label says; "2 Kilogram" is not.
///
/// Built from the enum rather than listed again here, so a unit added there appears in the picker
/// without anybody remembering to add it twice.
/// </summary>
public sealed record InventoryUnitOption(string Value, string Name, string ShortName)
{
    public static readonly IReadOnlyList<InventoryUnitOption> All =
    [
        .. Enum.GetValues<InventoryUnit>()
            .Select(unit => new InventoryUnitOption(unit.ToString(), unit.ToString(), InventoryUnitShortForm.Of(unit)))
    ];

    public static readonly InventoryUnitOption Default = All[0];

    /// <summary>
    /// An unrecognised value - or none at all - reads as pieces rather than as nothing: the dropdown
    /// only offers the units above, and a row from somewhere else should still show an amount somebody
    /// can read. A private warehouse sealed before units existed is exactly the "none at all" case: its
    /// items carry no unit, and without this the picker showed pieces while the item held nothing, so
    /// the next save wrote that nothing back.
    /// </summary>
    public static InventoryUnitOption For(string? value)
        => All.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
            ?? Default;
}

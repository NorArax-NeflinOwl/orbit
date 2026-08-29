namespace Orbit.Web.Services;

/// <summary>
/// One selectable entry in an item's unit dropdown - pairs the wire value (matching
/// Orbit.Core.Inventory.InventoryUnit on the API side) with the full name shown while picking and the
/// short form written beside an amount. "2 kg" is what a shelf label says; "2 Kilogram" is not.
/// </summary>
public sealed record InventoryUnitOption(string Value, string Name, string ShortName)
{
    public static readonly IReadOnlyList<InventoryUnitOption> All =
    [
        new("Piece", "Piece", "pcs"),
        new("Kilogram", "Kilogram", "kg"),
        new("Milligram", "Milligram", "mg"),
        new("Litre", "Litre", "l"),
        new("Millilitre", "Millilitre", "ml"),
        new("Pack", "Pack", "pack")
    ];

    public static readonly InventoryUnitOption Default = All[0];

    /// <summary>
    /// An unrecognised value reads as pieces rather than as nothing at all: the dropdown only offers the
    /// units above, and a row from somewhere else should still show an amount somebody can read.
    /// </summary>
    public static InventoryUnitOption For(string value)
        => All.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
            ?? Default;
}

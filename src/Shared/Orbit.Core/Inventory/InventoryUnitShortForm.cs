namespace Orbit.Core.Inventory;

/// <summary>
/// How a unit is written beside an amount: "kg", not "Kilogram". Lives in Core because both sides need
/// the same list - the server writes it into a restock errand (<see cref="RestockTaskNaming.EntryFor"/>)
/// and the client both offers it in the item editor and reads it back to say it in the reader's language
/// (see Orbit.Web's InventoryUnitOption and OrbitWrittenNames). Two copies of this list would drift, and
/// an errand written with one and read with the other would show a unit nobody translated.
/// </summary>
public static class InventoryUnitShortForm
{
    public static string Of(InventoryUnit unit)
        => unit switch
        {
            InventoryUnit.Kilogram => "kg",
            InventoryUnit.Milligram => "mg",
            InventoryUnit.Litre => "l",
            InventoryUnit.Millilitre => "ml",
            InventoryUnit.Pack => "pack",
            _ => "pcs"
        };

    /// <summary>
    /// The unit this text is the short form of, or null when it is not one. What lets a reader translate
    /// the tail of an errand Orbit wrote without guessing at anything a person typed.
    /// </summary>
    public static InventoryUnit? Read(string shortForm)
    {
        foreach (var unit in Enum.GetValues<InventoryUnit>())
        {
            if (string.Equals(Of(unit), shortForm, StringComparison.OrdinalIgnoreCase))
            {
                return unit;
            }
        }

        return null;
    }
}

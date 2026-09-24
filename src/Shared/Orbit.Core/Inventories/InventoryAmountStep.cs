namespace Orbit.Core.Inventories;

/// <summary>
/// How much the two buttons on a shelf row move it by. In Orbit.Core so that both clients can read one
/// rule - although only the browser does today: the phone's own two buttons still move by a whole one,
/// which is a difference nobody has decided on and which is written down in `info/future-plan.md`.
///
/// Half a unit is right for the units a half of makes sense of - half a kilo, half a litre, half a
/// packet, half of one thing - and it is what the buttons did for everything until 2026-09-24. It is
/// absurd for the small ones: nothing is ever weighed out half a milligram at a time, and a row in
/// milligrams needed a hundred presses to move what one press should. So those move by fifty, which is
/// what was asked for.
///
/// Millilitres are in with milligrams although only grams were named: they are the same size of unit
/// with the same problem, and a rule that fixed one and left the other would be reported the same week.
/// </summary>
public static class InventoryAmountStep
{
    /// <summary>The small units, where a half is no use to anybody.</summary>
    private const decimal Fifty = 50m;

    private const decimal Half = 0.5m;

    public static decimal Of(InventoryUnit unit)
        => unit is InventoryUnit.Milligram or InventoryUnit.Millilitre ? Fifty : Half;

    /// <inheritdoc cref="Of(InventoryUnit)"/>
    /// <remarks>
    /// Taken as what a row actually carries - the wire value, or nothing at all on a shelf sealed
    /// before units existed. Anything unrecognised counts as pieces, which is the same answer the unit
    /// picker gives it.
    /// </remarks>
    public static decimal Of(string? unit)
        => Enum.TryParse<InventoryUnit>(unit, ignoreCase: true, out var known) ? Of(known) : Half;
}

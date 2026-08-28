namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// A colour an event can be marked with, and whether it is the one chosen. Orbit.Web offers any colour
/// at all through the browser's own picker; a phone has no such control, so it offers a palette - and
/// keeps whatever the browser set, as a swatch of its own, rather than losing it on the next save.
/// </summary>
/// <param name="Value">The hex the wire carries, or null for "no colour".</param>
public sealed record EventColourChoice(string? Value, bool IsChosen)
{
    /// <summary>
    /// Drawn from Orbit.Web's own tokens, so a colour picked here is one the browser already uses
    /// somewhere - see its app.css. Null leads, because most events have no colour.
    /// </summary>
    private static readonly string?[] Palette =
    [
        null, "#7260CB", "#A34D00", "#348F4F", "#C74B47", "#D9A514", "#2B7BB9", "#8E4A9E"
    ];

    public bool HasColour => Value is not null;

    public static IReadOnlyList<EventColourChoice> All(string? chosen)
    {
        var palette = Palette.Contains(chosen) ? Palette : [.. Palette, chosen];

        return [.. palette.Select(colour => new EventColourChoice(
            colour,
            string.Equals(colour, chosen, StringComparison.OrdinalIgnoreCase)))];
    }
}

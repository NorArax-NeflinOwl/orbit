using Orbit.Mobile.Localization;

namespace Orbit.Mobile.Screens.Calendar;

/// <summary>
/// One colour an event can be marked with, and whether it is the one in force. A swatch on screen -
/// which is why it carries a name as well as a value: a ring of coloured circles says nothing at all to
/// somebody who cannot see it.
/// </summary>
/// <param name="Name">Already in the reader's language, so the swatch itself needs no dictionary.</param>
public sealed record EventColourChoice(string? Value, bool IsChosen, string Name)
{
    /// <summary>
    /// Drawn from Orbit.Web's own tokens, so a colour picked here is one the browser already uses
    /// somewhere - see its app.css. Null leads, because most events have no colour.
    /// </summary>
    private static readonly string?[] Palette =
    [
        null, "#7260CB", "#A34D00", "#348F4F", "#C74B47", "#D9A514", "#2B7BB9", "#8E4A9E"
    ];

    /// <summary>
    /// What each of them is called. Kept beside the palette rather than derived from the hex, because
    /// naming a colour is a judgement about how it reads and not a calculation.
    /// </summary>
    private static readonly Dictionary<string, string> NamesByValue = new(StringComparer.OrdinalIgnoreCase)
    {
        ["#7260CB"] = "Purple",
        ["#A34D00"] = "Brown",
        ["#348F4F"] = "Green",
        ["#C74B47"] = "Red",
        ["#D9A514"] = "Amber",
        ["#2B7BB9"] = "Blue",
        ["#8E4A9E"] = "Violet"
    };

    public bool HasColour => Value is not null;

    public static IReadOnlyList<EventColourChoice> All(string? chosen, Translations translations)
    {
        var palette = Palette.Contains(chosen) ? Palette : [.. Palette, chosen];

        return [.. palette.Select(colour => new EventColourChoice(
            colour,
            string.Equals(colour, chosen, StringComparison.OrdinalIgnoreCase),
            NameOf(colour, translations)))];
    }

    /// <summary>
    /// A colour the event already carries but the palette does not offer - set in a browser, or left
    /// over from an older palette - is still shown and still has to be called something.
    /// </summary>
    private static string NameOf(string? colour, Translations translations)
        => colour is null ? translations["No colour"]
            : NamesByValue.TryGetValue(colour, out var name) ? translations[name]
            : translations["Another colour"];
}

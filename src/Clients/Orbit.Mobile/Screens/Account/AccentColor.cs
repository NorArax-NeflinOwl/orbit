using System.Globalization;

namespace Orbit.Mobile.Screens.Account;

/// <summary>
/// One colour the reader can pick Orbit's accent from. A hue rather than a full colour: the app's
/// accent tokens differ in lightness and chroma between the light and dark themes and among themselves,
/// so choosing the hue keeps every one of them in step instead of replacing four values with four more.
///
/// The same eight Orbit.Web offers, at the same hue angles - see its AccentColorService. A reader with
/// both should not find two different greens.
/// </summary>
/// <param name="Name">The English name shown in the picker, translated like any other text.</param>
/// <param name="Hue">The oklch hue angle, 0-360.</param>
public sealed record AccentColor(string Name, int Hue)
{
    /// <summary>
    /// The colours on offer, kept together rather than as a scatter of constants. Purple leads because
    /// it is what Orbit has always been, and is what a phone that has never chosen still gets.
    /// </summary>
    public static IReadOnlyList<AccentColor> All { get; } =
    [
        new("Purple", 288),
        new("Blue", 250),
        new("Teal", 195),
        new("Green", 150),
        new("Amber", 85),
        new("Orange", 55),
        new("Red", 25),
        new("Pink", 350)
    ];

    public static AccentColor Default => All[0];

    /// <summary>
    /// The colour stored under this hue, or the default. An unknown hue reads as the default rather
    /// than as itself: the picker only offers the eight above, and a stored value outside them would
    /// leave it showing nothing as chosen.
    /// </summary>
    public static AccentColor For(int hue)
        => All.FirstOrDefault(candidate => candidate.Hue == hue) ?? Default;
}

/// <summary>Remembers which accent the reader picked, across launches - see IThemeStore for the same shape.</summary>
public interface IAccentColorStore
{
    AccentColor Read();

    void Write(AccentColor accentColor);
}

/// <summary>
/// The four accent colours one hue produces, for one theme.
///
/// Written as hex rather than as a platform colour type on purpose: this project holds the screens, not
/// the app head, and the head is where a string becomes something that can be painted.
/// </summary>
/// <param name="Accent">What Orbit highlights with - buttons, links, the chosen chip.</param>
/// <param name="AccentHover">The same, one step further in, for something being pressed.</param>
/// <param name="AccentSubtle">A wash of it, for a surface that is tinted rather than filled.</param>
/// <param name="AccentOn">What is legible written on top of <paramref name="Accent"/>.</param>
public sealed record AccentPalette(string Accent, string AccentHover, string AccentSubtle, string AccentOn)
{
    /// <summary>
    /// The lightness and chroma of each token, which is what stays fixed while the hue moves. Taken
    /// from Orbit.Web's app.css so both clients draw the same colour from the same choice - it defines
    /// each token as an oklch() of the one hue for exactly this reason.
    /// </summary>
    private static readonly (double Lightness, double Chroma)[] Light =
        [(0.56, 0.16), (0.50, 0.17), (0.93, 0.035), (0.99, 0.005)];

    private static readonly (double Lightness, double Chroma)[] Dark =
        [(0.74, 0.13), (0.80, 0.11), (0.32, 0.06), (0.14, 0.02)];

    public static AccentPalette For(int hue, bool isDark)
    {
        var tokens = isDark ? Dark : Light;
        return new AccentPalette(
            Oklch.ToHex(tokens[0].Lightness, tokens[0].Chroma, hue),
            Oklch.ToHex(tokens[1].Lightness, tokens[1].Chroma, hue),
            Oklch.ToHex(tokens[2].Lightness, tokens[2].Chroma, hue),
            Oklch.ToHex(tokens[3].Lightness, tokens[3].Chroma, hue));
    }
}

/// <summary>
/// Turns an oklch colour into the hex a phone can paint. The browser does this itself - CSS takes
/// oklch() as written - and a phone has no such thing, so the same arithmetic has to live here for the
/// two clients to agree on what "green" means.
///
/// The conversion is Björn Ottosson's, which is the one the CSS specification defines oklch by.
/// </summary>
internal static class Oklch
{
    /// <param name="lightness">0-1, where 1 is white.</param>
    /// <param name="chroma">How far from grey, roughly 0-0.4 for colours a screen can show.</param>
    /// <param name="hueDegrees">Which colour, 0-360.</param>
    public static string ToHex(double lightness, double chroma, double hueDegrees)
    {
        var hueRadians = hueDegrees * Math.PI / 180;
        var a = chroma * Math.Cos(hueRadians);
        var b = chroma * Math.Sin(hueRadians);

        // Oklab is defined through the cube roots of three cone responses, so this steps back through
        // them: undo the cube root, then leave the cone space for linear sRGB.
        var longCone = Cubed(lightness + (0.3963377774 * a) + (0.2158037573 * b));
        var mediumCone = Cubed(lightness - (0.1055613458 * a) - (0.0638541728 * b));
        var shortCone = Cubed(lightness - (0.0894841775 * a) - (1.2914855480 * b));

        var red = (4.0767416621 * longCone) - (3.3077115913 * mediumCone) + (0.2309699292 * shortCone);
        var green = (-1.2684380046 * longCone) + (2.6097574011 * mediumCone) - (0.3413193965 * shortCone);
        var blue = (-0.0041960863 * longCone) - (0.7034186147 * mediumCone) + (1.7076147010 * shortCone);

        return $"#{Channel(red)}{Channel(green)}{Channel(blue)}";
    }

    private static double Cubed(double value) => value * value * value;

    /// <summary>
    /// One channel, gamma-encoded and clamped. A hue and chroma that land outside what a screen can
    /// show are clamped rather than refused - which is what a browser does with the same colour, and
    /// keeps the picker to eight choices rather than eight minus the ones some screen cannot manage.
    /// </summary>
    private static string Channel(double linear)
    {
        var encoded = linear <= 0.0031308
            ? 12.92 * linear
            : (1.055 * Math.Pow(linear, 1 / 2.4)) - 0.055;

        return ((int)Math.Round(Math.Clamp(encoded, 0, 1) * 255))
            .ToString("X2", CultureInfo.InvariantCulture);
    }
}

/// <summary>
/// One colour in the picker: what it is, what it is called, the hex its swatch is painted in, and
/// whether it is the one in force - a row of eight with none marked leaves the reader guessing.
/// </summary>
public sealed record AccentChoice(AccentColor Value, string Name, string Swatch, bool IsChosen);

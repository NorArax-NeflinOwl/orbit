using Orbit.Mobile.Screens.Account;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The accent the reader picks. A browser takes oklch() as written and works the colour out itself; a
/// phone has no such thing, so the same arithmetic lives in the app - which is only worth anything if
/// it agrees with what the browser produces.
/// </summary>
public sealed class AccentColorTests
{
    /// <summary>
    /// The eight colours Colors.xaml has carried since before any of this was configurable, worked out
    /// from the one hue. They were written by hand from Orbit.Web's own tokens, so reproducing them
    /// exactly is what says the conversion here matches the one a browser does - and it does, to the
    /// byte. If this ever fails, the phone and the browser have started drawing different purples.
    /// </summary>
    [Fact]
    public void The_default_hue_produces_the_colours_Orbit_has_always_been()
    {
        var light = AccentPalette.For(AccentColor.Default.Hue, isDark: false);
        var dark = AccentPalette.For(AccentColor.Default.Hue, isDark: true);

        Assert.Equal(new AccentPalette("#7260CB", "#624BBC", "#E6E5FF", "#FBFBFF"), light);
        Assert.Equal(new AccentPalette("#A79DF8", "#BAB2FF", "#312D50", "#090811"), dark);
    }

    /// <summary>The same eight Orbit.Web offers, at the same angles - a reader with both must not find two greens.</summary>
    [Fact]
    public void The_colours_on_offer_are_the_ones_the_web_offers()
    {
        Assert.Equal(
            [("Purple", 288), ("Blue", 250), ("Teal", 195), ("Green", 150),
             ("Amber", 85), ("Orange", 55), ("Red", 25), ("Pink", 350)],
            AccentColor.All.Select(accent => (accent.Name, accent.Hue)));
    }

    [Fact]
    public void Every_colour_on_offer_produces_a_readable_hex()
        => Assert.All(
            AccentColor.All,
            accent =>
            {
                var palette = AccentPalette.For(accent.Hue, isDark: false);
                Assert.Matches("^#[0-9A-F]{6}$", palette.Accent);
                Assert.Matches("^#[0-9A-F]{6}$", palette.AccentOn);
            });

    /// <summary>
    /// Every hue has to give a different colour, or the picker offers eight ways to choose the same
    /// thing. Worth a test because the arithmetic clamps: a hue and chroma outside what a screen can
    /// show come back clamped, and enough clamping would collapse neighbours into each other.
    /// </summary>
    [Fact]
    public void No_two_colours_on_offer_come_out_the_same()
    {
        var accents = AccentColor.All.Select(accent => AccentPalette.For(accent.Hue, isDark: false).Accent);

        Assert.Equal(AccentColor.All.Count, accents.Distinct().Count());
    }

    /// <summary>
    /// A hue nobody offers reads as the default rather than as itself: the picker shows the eight
    /// above, and a stored value outside them would leave it showing nothing as chosen.
    /// </summary>
    [Fact]
    public void A_hue_that_is_not_on_offer_reads_as_the_default()
    {
        Assert.Equal(AccentColor.Default, AccentColor.For(999));
        Assert.Equal("Teal", AccentColor.For(195).Name);
    }

    /// <summary>
    /// The dark theme is not the light one dimmed: each token has its own lightness and chroma there,
    /// which is why the palette takes the theme rather than being worked out once.
    /// </summary>
    [Fact]
    public void The_dark_theme_gets_its_own_colours()
        => Assert.NotEqual(AccentPalette.For(150, isDark: false), AccentPalette.For(150, isDark: true));
}

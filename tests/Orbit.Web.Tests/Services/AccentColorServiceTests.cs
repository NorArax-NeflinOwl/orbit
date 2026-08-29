using Bunit;
using Orbit.Web.Services;
using Xunit;

namespace Orbit.Web.Tests.Services;

/// <summary>
/// The one colour the reader picks Orbit's accent from. Kept on the device, like the theme, so what
/// matters here is that a choice made yesterday is still the choice today and that an unreadable stored
/// value falls back to what Orbit has always been rather than to nothing.
/// </summary>
public sealed class AccentColorServiceTests : IDisposable
{
    private readonly TestContext _context = new();

    public AccentColorServiceTests()
    {
        var module = _context.JSInterop.SetupModule("./js/accentColor.js");
        module.SetupVoid("setStoredAccentHue", _ => true).SetVoidResult();
        module.SetupVoid("applyAccentHue", _ => true).SetVoidResult();
    }

    [Fact]
    public void Purple_is_what_an_account_that_has_never_chosen_gets()
    {
        // What Orbit has always been. It leads the list for the same reason.
        Assert.Equal("Purple", AccentColor.Default.Name);
        Assert.Equal(AccentColor.Default, AccentColor.All[0]);
    }

    [Fact]
    public void No_two_colours_on_offer_are_the_same_colour()
    {
        // A hue is what a swatch stands for, so two swatches sharing one is two ways to pick the same
        // thing - and one of them can never be shown as chosen.
        Assert.Equal(AccentColor.All.Count, AccentColor.All.Select(colour => colour.Hue).Distinct().Count());
    }

    [Fact]
    public async Task A_colour_chosen_earlier_is_the_one_that_comes_back()
    {
        StoredHue("195");
        var accentColors = new AccentColorService(_context.JSInterop.JSRuntime);

        await accentColors.InitializeAsync();

        Assert.Equal("Teal", accentColors.Current.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not a number")]
    [InlineData("7")]
    public async Task A_stored_value_that_names_no_colour_on_offer_reads_as_the_default(string? stored)
    {
        // The picker only offers the eight, so anything else would leave it showing nothing as chosen.
        StoredHue(stored);
        var accentColors = new AccentColorService(_context.JSInterop.JSRuntime);

        await accentColors.InitializeAsync();

        Assert.Equal(AccentColor.Default, accentColors.Current);
    }

    [Fact]
    public async Task Choosing_a_colour_stores_it_applies_it_and_says_so()
    {
        var accentColors = new AccentColorService(_context.JSInterop.JSRuntime);
        var raised = 0;
        accentColors.Changed += () => raised++;

        await accentColors.SetAsync(AccentColor.All.First(colour => colour.Name == "Amber"));

        Assert.Equal("Amber", accentColors.Current.Name);
        Assert.Equal(1, raised);
        Assert.Contains(
            _context.JSInterop.Invocations,
            invocation => invocation.Identifier == "setStoredAccentHue" && Argument(invocation) == "85");
        Assert.Contains(
            _context.JSInterop.Invocations,
            invocation => invocation.Identifier == "applyAccentHue" && Argument(invocation) == "85");
    }

    /// <summary>The hue as it travels to JS - a plain string, and invariant, so no locale can bend it.</summary>
    private static string? Argument(JSRuntimeInvocation invocation) => invocation.Arguments[0] as string;

    private void StoredHue(string? hue)
        => _context.JSInterop.SetupModule("./js/accentColor.js")
            .Setup<string?>("getStoredAccentHue")
            .SetResult(hue);

    public void Dispose() => _context.Dispose();
}

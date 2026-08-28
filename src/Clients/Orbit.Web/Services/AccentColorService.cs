using System.Globalization;
using Microsoft.JSInterop;

namespace Orbit.Web.Services;

/// <summary>
/// One colour the reader can pick Orbit's accent from. A hue rather than a full colour: the app's
/// accent tokens differ in lightness and chroma between the light and dark themes and among themselves
/// (see app.css), so choosing the hue keeps every one of them in step instead of replacing four values
/// with four more.
/// </summary>
/// <param name="Name">The English name shown in the picker, translated like any other text.</param>
/// <param name="Hue">The oklch hue angle, 0-360.</param>
public sealed record AccentColor(string Name, int Hue)
{
    /// <summary>
    /// The colours on offer, kept together rather than as a scatter of constants. Purple leads because
    /// it is what Orbit has always been, and is what an account that has never chosen still gets.
    /// </summary>
    public static readonly IReadOnlyList<AccentColor> All =
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

    public static readonly AccentColor Default = All[0];
}

/// <summary>
/// Reads and persists the reader's accent colour (see wwwroot/js/accentColor.js) and applies it to the
/// document. Mirrors <see cref="ThemeService"/>, including the part where index.html's inline
/// anti-flash script has already applied whatever was stored before this service - or Blazor itself -
/// has loaded, so initialising only has to sync <see cref="Current"/> to it.
///
/// Kept on the device rather than on the account, for the same reason the theme is: it is how Orbit
/// looks on this screen.
/// </summary>
public sealed class AccentColorService(IJSRuntime jsRuntime)
{
    public AccentColor Current { get; private set; } = AccentColor.Default;

    /// <summary>Raised after <see cref="SetAsync"/> applies a colour, so the page showing the choice can refresh.</summary>
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        await using var module = await ImportModuleAsync();
        var stored = await module.InvokeAsync<string?>("getStoredAccentHue");
        Current = ToAccentColor(stored);
    }

    public async Task SetAsync(AccentColor accentColor)
    {
        Current = accentColor;
        var hue = accentColor.Hue.ToString(CultureInfo.InvariantCulture);

        await using var module = await ImportModuleAsync();
        await module.InvokeVoidAsync("setStoredAccentHue", hue);
        await module.InvokeVoidAsync("applyAccentHue", hue);

        Changed?.Invoke();
    }

    /// <summary>
    /// An unreadable or unknown hue reads as the default rather than as itself: the picker only offers
    /// the colours above, and a stored value outside them would leave it showing nothing as chosen.
    /// </summary>
    private static AccentColor ToAccentColor(string? stored)
        => int.TryParse(stored, NumberStyles.Integer, CultureInfo.InvariantCulture, out var hue)
            ? AccentColor.All.FirstOrDefault(candidate => candidate.Hue == hue) ?? AccentColor.Default
            : AccentColor.Default;

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/accentColor.js");
}

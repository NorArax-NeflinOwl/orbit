using Microsoft.JSInterop;

namespace Orbit.Web.Services;

public enum ThemePreference
{
    Light,
    Dark,

    /// <summary>Follow the OS-level color scheme (prefers-color-scheme) instead of a fixed choice.</summary>
    System
}

/// <summary>
/// Reads and persists the user's light/dark theme preference (see wwwroot/js/theme.js) and applies it to
/// the document. index.html's inline anti-flash script already resolves and applies a theme before this
/// service - or Blazor itself - has loaded, so InitializeAsync only needs to sync <see cref="Current"/> to
/// whatever was actually stored, not re-apply anything.
/// </summary>
public sealed class ThemeService(IJSRuntime jsRuntime)
{
    public ThemePreference Current { get; private set; } = ThemePreference.System;

    /// <summary>Raised after <see cref="SetAsync"/> changes and applies the theme, so pages showing the current choice (Options) can refresh.</summary>
    public event Action? Changed;

    public async Task InitializeAsync()
    {
        await using var module = await ImportModuleAsync();
        var stored = await module.InvokeAsync<string?>("getStoredTheme");
        Current = ToPreference(stored);
    }

    /// <summary>
    /// Whether the page is dark right now - the stored choice where there is one, and the browser's own
    /// answer where it is "follow the system". Asked by anything that has to start from what is on
    /// screen rather than from what was chosen: the map's own light/night switch, which begins at
    /// whatever the map already looks like.
    /// </summary>
    public async Task<bool> IsDarkNowAsync()
    {
        if (Current is not ThemePreference.System)
        {
            return Current is ThemePreference.Dark;
        }

        await using var module = await ImportModuleAsync();
        return await module.InvokeAsync<bool>("systemPrefersDark");
    }

    public async Task SetAsync(ThemePreference preference)
    {
        Current = preference;
        var value = ToStoredValue(preference);

        await using var module = await ImportModuleAsync();
        await module.InvokeVoidAsync("setStoredTheme", value);
        await module.InvokeVoidAsync("applyTheme", value);

        Changed?.Invoke();
    }

    private static ThemePreference ToPreference(string? stored) => stored switch
    {
        "light" => ThemePreference.Light,
        "dark" => ThemePreference.Dark,
        _ => ThemePreference.System
    };

    private static string? ToStoredValue(ThemePreference preference) => preference switch
    {
        ThemePreference.Light => "light",
        ThemePreference.Dark => "dark",
        _ => null
    };

    private async Task<IJSObjectReference> ImportModuleAsync()
        => await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/theme.js");
}

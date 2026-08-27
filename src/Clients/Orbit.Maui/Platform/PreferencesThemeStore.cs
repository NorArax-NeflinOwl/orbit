using Orbit.Mobile.Screens.Account;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps the chosen theme in <see cref="IPreferences"/>, beside the language and the chosen
/// availability - it is a preference about this device and says nothing about the account.
/// </summary>
public sealed class PreferencesThemeStore : IThemeStore
{
	private const string ThemeKey = "orbit.appearance.theme";

	private readonly IPreferences _preferences;

	public PreferencesThemeStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>Follows the phone unless something else was stored, which is what Orbit did before there was a choice.</summary>
	public ChosenTheme Read()
		=> Enum.TryParse<ChosenTheme>(_preferences.Get<string?>(ThemeKey, null), out var theme)
			? theme
			: ChosenTheme.System;

	public void Write(ChosenTheme theme) => _preferences.Set(ThemeKey, theme.ToString());
}

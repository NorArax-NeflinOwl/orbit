using System.Globalization;
using Orbit.Localization;
using Orbit.Mobile.Localization;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps the chosen language in <see cref="IPreferences"/>, beside the other choices that describe how
/// the app looks rather than who is signed in.
/// </summary>
public sealed class PreferencesLanguageStore : ILanguageStore
{
	private const string LanguageKey = "orbit.language";

	private readonly IPreferences _preferences;

	public PreferencesLanguageStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>
	/// Falls back to the phone's own language on a fresh install: somebody whose device is in Polish has
	/// already said which language they read.
	/// </summary>
	public AppLanguage Read()
	{
		if (Enum.TryParse<AppLanguage>(_preferences.Get<string?>(LanguageKey, null), out var chosen))
		{
			return chosen;
		}

		return CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "pl"
			? AppLanguage.Polish
			: AppLanguage.English;
	}

	public void Write(AppLanguage language) => _preferences.Set(LanguageKey, language.ToString());
}

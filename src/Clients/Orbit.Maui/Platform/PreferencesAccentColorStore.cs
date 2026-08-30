using System.Globalization;
using Orbit.Mobile.Screens.Account;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps the chosen accent in <see cref="IPreferences"/>, beside the theme and the language - it is a
/// preference about this device and says nothing about the account, exactly as it is on Orbit.Web.
/// </summary>
public sealed class PreferencesAccentColorStore : IAccentColorStore
{
	private const string HueKey = "orbit.appearance.accent-hue";

	private readonly IPreferences _preferences;

	public PreferencesAccentColorStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>
	/// The hue is stored rather than the name, so a colour renamed in a later build still comes back as
	/// the one the reader picked. An unreadable or unknown one reads as Orbit's own purple.
	/// </summary>
	public AccentColor Read()
		=> int.TryParse(
			_preferences.Get<string?>(HueKey, null), NumberStyles.Integer, CultureInfo.InvariantCulture,
			out var hue)
			? AccentColor.For(hue)
			: AccentColor.Default;

	public void Write(AccentColor accentColor)
		=> _preferences.Set(HueKey, accentColor.Hue.ToString(CultureInfo.InvariantCulture));
}

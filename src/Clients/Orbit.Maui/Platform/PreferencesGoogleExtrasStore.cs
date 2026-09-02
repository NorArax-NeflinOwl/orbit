using Orbit.Mobile.Google;

namespace Orbit.Maui.Platform;

/// <inheritdoc cref="PreferencesDashboardPinStore"/>
public sealed class PreferencesGoogleExtrasStore : IGoogleExtrasStore
{
	private const string AllowedKey = "orbit.google.extras";

	private readonly IPreferences _preferences;

	public PreferencesGoogleExtrasStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>A phone nobody has answered for offers them, which is what they did before there was a switch.</summary>
	public bool Read() => _preferences.Get(AllowedKey, true);

	public void Write(bool isAllowed) => _preferences.Set(AllowedKey, isAllowed);
}

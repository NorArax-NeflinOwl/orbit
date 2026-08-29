using Orbit.Mobile.Presence;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps the chosen availability in <see cref="IPreferences"/>. Not the secure store: it says nothing
/// about the account and would not matter if it leaked - the same reasoning as
/// <see cref="PreferencesVersionVerdictCache"/>.
/// </summary>
public sealed class PreferencesPresenceStore : IPresenceStore
{
	private const string ChosenKey = "orbit.presence.chosen";

	private readonly IPreferences _preferences;

	public PreferencesPresenceStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>Available unless something else was stored - a fresh install should not start silent.</summary>
	public ChosenAvailability Read()
		=> Enum.TryParse<ChosenAvailability>(_preferences.Get<string?>(ChosenKey, null), out var chosen)
			? chosen
			: ChosenAvailability.Available;

	public void Write(ChosenAvailability availability) => _preferences.Set(ChosenKey, availability.ToString());
}

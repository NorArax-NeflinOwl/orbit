using Orbit.Mobile.Screens.Dashboard;

namespace Orbit.Maui.Platform;

/// <inheritdoc cref="PreferencesDashboardPinStore"/>
public sealed class PreferencesUpcomingHorizonStore : IUpcomingHorizonStore
{
	private const string DaysKey = "orbit.dashboard.upcoming-days";

	private readonly IPreferences _preferences;

	public PreferencesUpcomingHorizonStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>Null while nothing is stored, which is the default horizon rather than no horizon.</summary>
	public int? ReadDays() => _preferences.ContainsKey(DaysKey) ? _preferences.Get(DaysKey, UpcomingHorizon.DefaultDays) : null;

	public void WriteDays(int days) => _preferences.Set(DaysKey, days);
}

using Orbit.Mobile.Screens.Dashboard;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps the pinned dashboard cards in <see cref="IPreferences"/>, next to the language and the chosen
/// availability. Not the secure store and not the database: it is one page's layout on one device, and
/// it says nothing about the account - the same reasoning as <see cref="PreferencesPresenceStore"/>.
/// </summary>
public sealed class PreferencesDashboardPinStore : IDashboardPinStore
{
	private const string PinnedKey = "orbit.dashboard.pinned";

	private readonly IPreferences _preferences;

	public PreferencesDashboardPinStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>An unknown name is dropped rather than throwing: a card renamed in a later build is not a crash.</summary>
	public IReadOnlySet<DashboardCardKind> Read()
		=> (_preferences.Get<string?>(PinnedKey, null) ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(name => Enum.TryParse<DashboardCardKind>(name, out var kind) ? kind : (DashboardCardKind?)null)
			.Where(kind => kind is not null)
			.Select(kind => kind!.Value)
			.ToHashSet();

	public void Write(IReadOnlySet<DashboardCardKind> pinned)
		=> _preferences.Set(PinnedKey, string.Join(',', pinned.Select(kind => kind.ToString())));
}

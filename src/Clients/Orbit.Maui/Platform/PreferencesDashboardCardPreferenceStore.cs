using Orbit.Mobile.Screens.Dashboard;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps the dashboard parts this reader has put away in <see cref="IPreferences"/>, beside the pins -
/// see <see cref="PreferencesDashboardPinStore"/>, which this deliberately mirrors. It is one page's
/// layout on one device and says nothing about the account.
/// </summary>
public sealed class PreferencesDashboardCardPreferenceStore : IDashboardCardPreferenceStore
{
	private const string HiddenKey = "orbit.dashboard.hidden";
	private const string FiltersKey = "orbit.dashboard.filters";

	private readonly IPreferences _preferences;

	public PreferencesDashboardCardPreferenceStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>An unknown name is dropped rather than throwing: a card renamed in a later build is not a crash.</summary>
	public IReadOnlySet<DashboardCardKind> ReadHidden()
		=> (_preferences.Get<string?>(HiddenKey, null) ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(name => Enum.TryParse<DashboardCardKind>(name, out var kind) ? kind : (DashboardCardKind?)null)
			.Where(kind => kind is not null)
			.Select(kind => kind!.Value)
			.ToHashSet();

	public void WriteHidden(IReadOnlySet<DashboardCardKind> hidden)
		=> _preferences.Set(HiddenKey, string.Join(',', hidden.Select(kind => kind.ToString())));

	/// <summary>
	/// Stored as "Kind=Filter" pairs on one line, the same shape as the hidden list above. A pair whose
	/// card or filter this build does not know is dropped rather than throwing - the same reasoning as
	/// there, and the card simply shows everything.
	/// </summary>
	public IReadOnlyDictionary<DashboardCardKind, DashboardCardFilter> ReadFilters()
		=> (_preferences.Get<string?>(FiltersKey, null) ?? string.Empty)
			.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
			.Select(pair => pair.Split('=', 2))
			.Where(parts => parts.Length == 2
				&& Enum.TryParse<DashboardCardKind>(parts[0], out _)
				&& Enum.TryParse<DashboardCardFilter>(parts[1], out _))
			.ToDictionary(
				parts => Enum.Parse<DashboardCardKind>(parts[0]),
				parts => Enum.Parse<DashboardCardFilter>(parts[1]));

	public void WriteFilters(IReadOnlyDictionary<DashboardCardKind, DashboardCardFilter> filters)
		=> _preferences.Set(
			FiltersKey,
			string.Join(',', filters.Select(filter => $"{filter.Key}={filter.Value}")));
}

using Orbit.Mobile.Screens;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps how each list screen is being read on the device - beside the dashboard's pins and the
/// calendar's own reading order, and for the same reason. See <see cref="IListArrangementStore"/>.
/// </summary>
public sealed class PreferencesListArrangementStore : IListArrangementStore
{
	private readonly IPreferences _preferences;

	public PreferencesListArrangementStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>A value written by a build that offered a different set reads as the default.</summary>
	public ListArrangement Read(ListSection section)
		=> new(
			Enum.TryParse<ListSortOrder>(_preferences.Get<string?>(SortKey(section), null), out var order)
				? order
				: ListArrangement.Default.SortOrder,
			Enum.TryParse<ListFilter>(_preferences.Get<string?>(FilterKey(section), null), out var filter)
				? filter
				: ListArrangement.Default.Filter);

	public void Write(ListSection section, ListArrangement arrangement)
	{
		_preferences.Set(SortKey(section), arrangement.SortOrder.ToString());
		_preferences.Set(FilterKey(section), arrangement.Filter.ToString());
	}

	private static string SortKey(ListSection section) => $"orbit.list.{section}.sort-order";

	private static string FilterKey(ListSection section) => $"orbit.list.{section}.filter";
}

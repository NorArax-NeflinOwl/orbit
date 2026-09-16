using System.Text.Json;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps this phone's copy of the account's Tasks card filters, and which one the card shows, in
/// <see cref="IPreferences"/> - see TaskTagFilters, which says why a copy rather than a table.
/// </summary>
public sealed class PreferencesTaskTagFilterStore : ITaskTagFilterStore
{
	private const string FiltersKey = "orbit.dashboard.task-tag-filters";
	private const string ChosenKey = "orbit.dashboard.task-tag-filter";

	private readonly IPreferences _preferences;

	public PreferencesTaskTagFilterStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>A copy that no longer reads is no filters rather than a crash - the next sync writes it again.</summary>
	public IReadOnlyList<TaskTagFilterDto> ReadFilters()
	{
		try
		{
			return JsonSerializer.Deserialize<List<TaskTagFilterDto>>(_preferences.Get(FiltersKey, "[]")) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	public void WriteFilters(IReadOnlyList<TaskTagFilterDto> filters)
		=> _preferences.Set(FiltersKey, JsonSerializer.Serialize(filters));

	public Guid? ReadChosen()
		=> Guid.TryParse(_preferences.Get<string?>(ChosenKey, null), out var chosen) ? chosen : null;

	public void WriteChosen(Guid? filterId)
	{
		if (filterId is { } chosen)
		{
			_preferences.Set(ChosenKey, chosen.ToString());
		}
		else
		{
			_preferences.Remove(ChosenKey);
		}
	}
}

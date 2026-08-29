using System.Text.Json;
using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps how the reader arranges their task lists in <see cref="IPreferences"/>, beside the theme and
/// the language - it is a preference about this device and says nothing about the lists.
/// </summary>
public sealed class PreferencesTaskListArrangementStore : ITaskListArrangementStore
{
	private const string SortOrderKey = "orbit.tasks.sort-order";
	private const string ManualOrderKey = "orbit.tasks.manual-order";

	private readonly IPreferences _preferences;

	public PreferencesTaskListArrangementStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>What matters most first, until the reader says otherwise - the order Orbit opened on.</summary>
	public TaskListSortOrder ReadSortOrder()
		=> Enum.TryParse<TaskListSortOrder>(_preferences.Get<string?>(SortOrderKey, null), out var sortOrder)
			? sortOrder
			: TaskListSortOrder.Priority;

	public void WriteSortOrder(TaskListSortOrder sortOrder)
		=> _preferences.Set(SortOrderKey, sortOrder.ToString());

	/// <summary>
	/// Nothing written, or something written by a build that stored it differently, means nothing has
	/// been moved yet - which is a fine answer and the one every phone starts on.
	/// </summary>
	public IReadOnlyList<Guid> ReadManualOrder()
	{
		if (_preferences.Get<string?>(ManualOrderKey, null) is not { Length: > 0 } stored)
		{
			return [];
		}

		try
		{
			return JsonSerializer.Deserialize<List<Guid>>(stored) ?? [];
		}
		catch (JsonException)
		{
			return [];
		}
	}

	public void WriteManualOrder(IReadOnlyList<Guid> orderedLocalIds)
		=> _preferences.Set(ManualOrderKey, JsonSerializer.Serialize(orderedLocalIds));
}

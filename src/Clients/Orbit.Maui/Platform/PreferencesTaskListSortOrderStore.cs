using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps how the reader arranges their task lists in <see cref="IPreferences"/>, beside the theme and
/// the language - it is a preference about this device and says nothing about the lists.
/// </summary>
public sealed class PreferencesTaskListSortOrderStore : ITaskListSortOrderStore
{
	private const string SortOrderKey = "orbit.tasks.sort-order";

	private readonly IPreferences _preferences;

	public PreferencesTaskListSortOrderStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>What matters most first, until the reader says otherwise - the order Orbit opened on.</summary>
	public TaskListSortOrder Read()
		=> Enum.TryParse<TaskListSortOrder>(_preferences.Get<string?>(SortOrderKey, null), out var sortOrder)
			? sortOrder
			: TaskListSortOrder.Priority;

	public void Write(TaskListSortOrder sortOrder) => _preferences.Set(SortOrderKey, sortOrder.ToString());
}

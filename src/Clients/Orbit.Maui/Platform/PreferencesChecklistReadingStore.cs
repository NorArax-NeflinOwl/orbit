using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps how each checklist is read in the device's own preferences, beside the dashboard's pins and
/// for the same reason - see <see cref="IChecklistReadingStore"/>.
/// </summary>
public sealed class PreferencesChecklistReadingStore : IChecklistReadingStore
{
	private const string KeyPrefix = "orbit.checklist.reading.";

	private readonly IPreferences _preferences;

	public PreferencesChecklistReadingStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>
	/// Anything that cannot be read is the default rather than an error: a preference written by an
	/// older build, or half of one, is not worth refusing to open a list over.
	/// </summary>
	public ChecklistReading Read(Guid taskListLocalId)
	{
		var saved = _preferences.Get<string?>(KeyOf(taskListLocalId), null);
		if (saved is null || saved.Split(',') is not [var folded, var order])
		{
			return ChecklistReading.Default;
		}

		return new ChecklistReading(
			bool.TryParse(folded, out var isFolded) && isFolded,
			Enum.TryParse<StockCheckOrder>(order, out var stockOrder) ? stockOrder : StockCheckOrder.AsCounted);
	}

	public void Write(Guid taskListLocalId, ChecklistReading reading)
		=> _preferences.Set(KeyOf(taskListLocalId), $"{reading.IsStockCheckFolded},{reading.StockOrder}");

	/// <summary>One key per list: a list deleted takes its own preference with it and nothing else.</summary>
	private static string KeyOf(Guid taskListLocalId) => KeyPrefix + taskListLocalId.ToString("N");
}

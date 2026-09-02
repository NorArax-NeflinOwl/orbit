using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Maui.Platform;

/// <summary>
/// Keeps what order the calendar's list is read in on the device, beside the dashboard's pins and for
/// the same reason - see <see cref="ICalendarListOrderStore"/>.
/// </summary>
public sealed class PreferencesCalendarListOrderStore : ICalendarListOrderStore
{
	private const string SortOrderKey = "orbit.calendar.list.sort-order";

	private readonly IPreferences _preferences;

	public PreferencesCalendarListOrderStore(IPreferences preferences) => _preferences = preferences;

	/// <summary>An order written by a build that offered a different set reads as the default.</summary>
	public CalendarListSortOrder Read()
		=> Enum.TryParse<CalendarListSortOrder>(_preferences.Get<string?>(SortOrderKey, null), out var stored)
			? stored
			: CalendarListSortOrder.When;

	public void Write(CalendarListSortOrder sortOrder) => _preferences.Set(SortOrderKey, sortOrder.ToString());
}

using System.Globalization;

namespace Orbit.Maui.Controls;

/// <summary>
/// Turns an event's chosen colour into the paint for the dot beside it. Lives here rather than on the
/// view model for the same reason as <see cref="PresenceColorConverter"/>: the view model holds what
/// the event says, and this decides how it looks.
///
/// An event with no colour of its own falls back to the app's accent, which is what Orbit.Web does with
/// the same dot (see its Dashboard.razor). The fallback cannot be decided further up: it differs
/// between the light and dark themes, and a view model that reached for a theme colour would be
/// reaching into the platform.
/// </summary>
public sealed class EventColourConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is string chosen && Color.TryParse(chosen, out var colour))
		{
			return colour;
		}

		var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
		return Application.Current?.Resources[isDark ? "PrimaryDark" : "Primary"] ?? Colors.MediumPurple;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("A colour dot is never read back off the screen.");
}

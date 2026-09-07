using System.Globalization;

namespace Orbit.Maui.Controls;

/// <summary>
/// Turns an event's chosen colour into the brush for the dot beside it. Lives here rather than on the
/// view model for the same reason as <see cref="PresenceColorConverter"/>: the view model holds what
/// the event says, and this decides how it looks.
///
/// An event with no colour of its own falls back to the app's accent, which is what Orbit.Web does with
/// the same dot (see its Dashboard.razor). The fallback cannot be decided further up: it differs
/// between the light and dark themes and between palettes, and a view model that reached for a theme
/// colour would be reaching into the platform.
/// </summary>
public sealed class EventColourConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		if (value is string chosen && Color.TryParse(chosen, out var colour))
		{
			return Paint(colour);
		}

		// "Accent" and not one of the Light/Dark pairs: it is the one App keeps current for whichever
		// theme and whichever palette is in force (see App.ApplyPalette), which is what var(--accent)
		// is on the other client. Asked for rather than indexed, because the indexer throws on a key
		// that is not there and a converter that throws leaves the shape unpainted rather than saying
		// anything - which is how the dot came to take up its place in the row and draw nothing at all.
		return Application.Current?.Resources.TryGetValue("Accent", out var accent) is true && accent is Color fallback
			? Paint(fallback)
			: Paint(Colors.MediumPurple);
	}

	/// <summary>
	/// A brush, not a colour. A Shape's Fill takes a Brush, and XAML's own Color-to-Brush conversion
	/// only happens where the value is written into the markup - a binding hands the value straight
	/// over, so a Color arrives as the wrong type and the shape is simply never painted. Which is what
	/// had happened: the dot beside a dashboard event took up its place in the row and drew nothing.
	/// </summary>
	private static Brush Paint(Color colour) => new SolidColorBrush(colour);

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("A colour dot is never read back off the screen.");
}

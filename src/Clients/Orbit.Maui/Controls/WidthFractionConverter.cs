using System.Globalization;

namespace Orbit.Maui.Controls;

/// <summary>
/// A fraction of something else's width, for the one place CSS has a unit and MAUI has not: a chat
/// bubble is at most 70% of the thread it is in (see app.css's .chat-bubble-row), and a layout
/// property takes a number of units rather than a proportion.
///
/// Bound to the thread's own Width, which is a bindable property, so the cap follows a rotation or a
/// window resize rather than being worked out once.
/// </summary>
public sealed class WidthFractionConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		// Before the first layout a width is -1, and a MaximumWidthRequest of -1 means "no cap" - which
		// is exactly right for that moment and wrong to guess anything else about.
		if (value is not double width || width <= 0)
		{
			return -1d;
		}

		var fraction = parameter is string written && double.TryParse(written, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
			? parsed
			: 0.7;

		return width * fraction;
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("A width is measured, never read back off a bubble.");
}

using System.Globalization;

namespace Orbit.Maui.Controls;

/// <summary>
/// The pin beside a note or task list: filled when it is pinned, outlined when it is not. Two glyphs
/// rather than one glyph and a colour, so the row still reads on a screen where the accent colour is
/// hard to tell from the text - and so it says which way the button goes rather than only what is.
/// </summary>
public sealed class PinGlyphConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is true ? "📌" : "📍";

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("A pin is never read back off the screen.");
}

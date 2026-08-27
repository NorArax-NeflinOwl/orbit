using System.Globalization;

namespace Orbit.Maui.Controls;

/// <summary>Shows an element only when the text bound to it says something.</summary>
public sealed class IsNotEmptyConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is string text && text.Length > 0;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("Visibility is never read back into text.");
}

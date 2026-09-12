using System.Globalization;

namespace Orbit.Maui.Controls;

/// <summary>
/// A tag's colour as its chip is drawn - see TagChipsView and TagsFieldView. The rule Orbit.Web's
/// .card-badge-tag-coloured follows: a wash of the colour behind the word, the colour itself as the edge,
/// and the page's own text colour on top, which keeps a pale yellow or a near-black readable in the light
/// theme and the dark one alike. ConverterParameter "edge" gives the outline, "solid" the whole colour (a
/// palette swatch), anything else the wash. A tag nobody coloured is drawn plain: no wash, a quiet edge.
///
/// A brush rather than a colour, for the reason EventColourConverter gives: a Border's Stroke and
/// Background take a Brush, and a bound Color arrives as the wrong type and paints nothing.
/// </summary>
public sealed class TagColourConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var part = parameter as string;
		if (value is string chosen && chosen.Length > 0 && Color.TryParse(chosen, out var colour))
		{
			return new SolidColorBrush(part switch
			{
				"edge" => colour.WithAlpha(0.8f),
				"solid" => colour,
				_ => colour.WithAlpha(0.22f)
			});
		}

		return new SolidColorBrush(part == "edge" ? Colors.Gray.WithAlpha(0.45f) : Colors.Transparent);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("A tag's colour is never read back off the screen.");
}

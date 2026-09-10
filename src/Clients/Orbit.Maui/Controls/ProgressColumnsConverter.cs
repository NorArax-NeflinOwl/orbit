using System.Globalization;

namespace Orbit.Maui.Controls;

/// <summary>
/// A fraction as two columns to lay a bar out in: the done part and the rest, in star units, so the
/// layout works out the widths and nothing has to be measured first.
///
/// For the design's progress bars - a hairline track with a coloured part lying over its left end.
/// MAUI's own <c>ProgressBar</c> draws Android's, whose track is a mid-grey the platform picks and
/// offers no way to change: three points of that across a card reads as a rule between two things
/// rather than as an empty bar. Two <c>BoxView</c>s and this are the whole of the alternative.
///
/// Columns rather than a width worked out from the track's own: a <c>MultiBinding</c> of a measured
/// width and a fraction never drew the filled half at all here, and star units need no measurement -
/// a bar of 1 star and 3 is a quarter done wherever the row turns out to be.
/// </summary>
public sealed class ProgressColumnsConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var done = value is double fraction ? Math.Clamp(fraction, 0, 1) : 0;

		return new ColumnDefinitionCollection(
			new ColumnDefinition(new GridLength(done, GridUnitType.Star)),
			new ColumnDefinition(new GridLength(1 - done, GridUnitType.Star)));
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("How far along a list is is read off the list, never off its bar.");
}

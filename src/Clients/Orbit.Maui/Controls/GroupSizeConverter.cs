using System.Globalization;
using Orbit.Mobile.Localization;

namespace Orbit.Maui.Controls;

/// <summary>
/// How many people a group holds, as the second line of its row.
///
/// Label first, count second: Polish declines the noun after a number, so "{0} members" cannot be
/// translated into one correct form. "People" rather than "Members": that word is the heading of the
/// roster on the web, where it is "Czlonkowie" - which is the wrong form to put a number after, and one
/// English string cannot be both.
/// </summary>
public sealed class GroupSizeConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var translations = IPlatformApplication.Current!.Services.GetRequiredService<Translations>();
		return $"{translations["People"]}: {value}";
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("A group's size is never read back off the screen.");
}

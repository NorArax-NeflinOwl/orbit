using System.Globalization;
using Orbit.Mobile.Localization;

namespace Orbit.Maui.Controls;

/// <summary>
/// How far one of the reader's own group messages got, in the same two words Orbit.Web uses: delivered
/// when the server has a copy addressed to every member, read only once every one of them has opened it.
/// </summary>
public sealed class GroupDeliveryConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		var translations = IPlatformApplication.Current!.Services.GetRequiredService<Translations>();
		return value is true ? translations["Read by everyone"] : translations["Delivered"];
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("A delivery line is never read back off the screen.");
}

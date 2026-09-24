using System.Globalization;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Localization;

namespace Orbit.Maui.Controls;

/// <summary>
/// The line over somebody's request to be allowed to change something of yours, which names what they
/// asked about - see <see cref="EditAccessRequest.AskedToEdit"/>, where the words are.
///
/// A converter because the row has no <see cref="Translations"/>: <see cref="ReadableChatMessage"/> is
/// read out of ciphertext in a project that knows nothing about a screen, and the kind has to be turned
/// into words somewhere between there and the label. The same way <see cref="GroupSizeConverter"/>
/// reaches for them.
/// </summary>
public sealed class AskedToEditConverter : IValueConverter
{
	public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
	{
		// Every message in the thread runs through this, and all but a request carry nothing.
		if (value is not EditAccessRequest request)
		{
			return string.Empty;
		}

		var translations = IPlatformApplication.Current!.Services.GetRequiredService<Translations>();
		return request.AskedToEdit(translations);
	}

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("A request is never read back off the screen.");
}

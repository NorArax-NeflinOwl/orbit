using System.Globalization;

namespace Orbit.Maui.Controls;

/// <summary>
/// Draws a picture from its bytes - what NoteLineRow.PictureBytes and SharedLine.PictureBytes hold. Bytes
/// rather than a file, because a private note's picture is kept sealed on the handset and only ever
/// opened into memory (see NotePictureCache); an Image is handed a stream over them and nothing else.
/// Null draws nothing, which is what the page shows a note in the picture's place for.
/// </summary>
public sealed class PictureBytesConverter : IValueConverter
{
	public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> value is byte[] { Length: > 0 } bytes ? ImageSource.FromStream(() => new MemoryStream(bytes)) : null;

	public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
		=> throw new NotSupportedException("A drawn picture is never read back into bytes.");
}

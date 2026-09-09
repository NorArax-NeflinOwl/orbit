namespace Orbit.Maui.Controls;

/// <summary>
/// Marks a field that is not a box and must be left bare.
///
/// A note's lines are written in <c>Entry</c>s so they can be corrected where they are read, and a note
/// drawn as a stack of boxes is a form rather than a note. Such a field gets nothing at all - not
/// Orbit's hairline, and not Android's own line under the text either.
///
/// Read on Android by <c>Platforms/Android/FieldBox.cs</c>, which is where the drawing happens. The
/// property lives here rather than beside it because the pages that set it are shared by both heads,
/// and XAML compiled for iOS cannot name an Android-only type.
///
/// It used to be read off the field's transparency. That stopped meaning anything when the redesign
/// made every field transparent - a box is a hairline on the page now rather than a lifted surface, so
/// "asked for no fill" no longer distinguishes a note's line from an ordinary field. Said outright
/// instead.
/// </summary>
public static class BareField
{
	public static readonly BindableProperty IsBareProperty =
		BindableProperty.CreateAttached("IsBare", typeof(bool), typeof(BareField), false);

	public static bool GetIsBare(BindableObject field) => (bool)field.GetValue(IsBareProperty);

	public static void SetIsBare(BindableObject field, bool value) => field.SetValue(IsBareProperty, value);
}

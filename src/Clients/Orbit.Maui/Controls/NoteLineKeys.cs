using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// Marks a field that is one line of a longer piece of writing, and says what to do when the reader
/// presses backspace at its very start.
///
/// The note editor is one surface made of one field per line - a line can carry a real tick box, and no
/// text box can hold a control. Everything else about it has to behave like a single text box, and the
/// half that does not come free is this one: backspace at the head of a line joins it to the line above.
/// Without it a line could be emptied and never got rid of.
///
/// Read on Android by <c>Platforms/Android/NoteLineBackspace.cs</c>, which is where the key actually
/// arrives. The property lives here rather than beside it because the page that sets it is shared by
/// both heads, and code compiled for iOS cannot name an Android-only type. On a head that does not
/// implement it, nothing happens - which is the same as the editor not having the behaviour, rather
/// than the editor being broken.
/// </summary>
public static class NoteLineKeys
{
	public static readonly BindableProperty JoinsTheLineAboveProperty =
		BindableProperty.CreateAttached("JoinsTheLineAbove", typeof(ICommand), typeof(NoteLineKeys), null);

	public static ICommand? GetJoinsTheLineAbove(BindableObject field)
		=> (ICommand?)field.GetValue(JoinsTheLineAboveProperty);

	public static void SetJoinsTheLineAbove(BindableObject field, ICommand? value)
		=> field.SetValue(JoinsTheLineAboveProperty, value);
}

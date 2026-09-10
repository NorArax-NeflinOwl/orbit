using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// Marks a field that is one line of a longer piece of writing, and says what to do with the keys that
/// mean something to the writing rather than to that one line.
///
/// The note editor is one surface made of one field per line - a line can carry a real tick box, and no
/// text box can hold a control. Everything else about it has to behave like a single text box, and that
/// is the half that does not come free: backspace at the head of a line joins it to the line above
/// (without it a line could be emptied and never got rid of), and the arrows walk the caret between
/// lines instead of stopping at the end of the one it is in.
///
/// Read on Android by <c>Platforms/Android/NoteLineKeyPresses.cs</c>, which is where the keys actually
/// arrive, and given the field they came from as their parameter. The properties live here rather than
/// beside it because the page that sets them is shared by both heads, and code compiled for iOS cannot
/// name an Android-only type. On a head that does not implement it, nothing happens - which is the same
/// as the editor not having the behaviour, rather than the editor being broken.
///
/// Set them in the markup, not in code once the field has loaded: the Android half decides whether to
/// listen for the keys while the field's handler is being built, which is earlier than that, and a field
/// told afterwards is never listened to at all.
/// </summary>
public static class NoteLineKeys
{
	public static readonly BindableProperty JoinsTheLineAboveProperty =
		BindableProperty.CreateAttached("JoinsTheLineAbove", typeof(ICommand), typeof(NoteLineKeys), null);

	public static ICommand? GetJoinsTheLineAbove(BindableObject field)
		=> (ICommand?)field.GetValue(JoinsTheLineAboveProperty);

	public static void SetJoinsTheLineAbove(BindableObject field, ICommand? value)
		=> field.SetValue(JoinsTheLineAboveProperty, value);

	/// <summary>Arrow up: the caret leaves this line for the one over it, keeping the column it was in.</summary>
	public static readonly BindableProperty GoesToTheLineAboveProperty =
		BindableProperty.CreateAttached("GoesToTheLineAbove", typeof(ICommand), typeof(NoteLineKeys), null);

	public static ICommand? GetGoesToTheLineAbove(BindableObject field)
		=> (ICommand?)field.GetValue(GoesToTheLineAboveProperty);

	public static void SetGoesToTheLineAbove(BindableObject field, ICommand? value)
		=> field.SetValue(GoesToTheLineAboveProperty, value);

	/// <summary>Arrow down: the same, downwards.</summary>
	public static readonly BindableProperty GoesToTheLineBelowProperty =
		BindableProperty.CreateAttached("GoesToTheLineBelow", typeof(ICommand), typeof(NoteLineKeys), null);

	public static ICommand? GetGoesToTheLineBelow(BindableObject field)
		=> (ICommand?)field.GetValue(GoesToTheLineBelowProperty);

	public static void SetGoesToTheLineBelow(BindableObject field, ICommand? value)
		=> field.SetValue(GoesToTheLineBelowProperty, value);
}

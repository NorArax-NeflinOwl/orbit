using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// Marks a control that means something different when it is held rather than pressed, and says what.
///
/// MAUI has no long-press gesture of its own: <c>TapGestureRecognizer</c> counts taps and nothing counts
/// time, and the usual answer - CommunityToolkit.Maui's <c>TouchBehavior</c> - is a package Orbit.Maui
/// does not reference and would not reference for one gesture. Android's own views have had a long click
/// since the beginning, so the gesture is read where it already exists: see
/// <c>Platforms/Android/LongPresses.cs</c>, which hooks the platform button behind whatever asked.
///
/// The property lives here rather than beside that file because the pages that set it are shared by both
/// heads, and code compiled for iOS cannot name an Android-only type. On a head that does not implement
/// it, holding does what pressing does - which is the gesture not being there rather than the control
/// being broken, and is why nothing may be reachable by holding alone.
///
/// Set it in the markup rather than in code once the control has loaded: the Android half decides whether
/// to listen while the handler is being built, and a control told afterwards is never listened to.
/// </summary>
public static class LongPress
{
	/// <summary>
	/// What holding this control does. The control's ordinary <c>CommandParameter</c> is what it is
	/// given, so one command serves a whole template and is told which row the press came from - the
	/// same arrangement <see cref="NoteLineKeys"/> uses for the keys.
	/// </summary>
	public static readonly BindableProperty CommandProperty =
		BindableProperty.CreateAttached("Command", typeof(ICommand), typeof(LongPress), null);

	public static ICommand? GetCommand(BindableObject control)
		=> (ICommand?)control.GetValue(CommandProperty);

	public static void SetCommand(BindableObject control, ICommand? value)
		=> control.SetValue(CommandProperty, value);
}

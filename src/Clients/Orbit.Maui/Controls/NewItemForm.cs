using System.Windows.Input;

namespace Orbit.Maui.Controls;

/// <summary>
/// The plus at the head of a list screen, and what it opens.
///
/// Orbit.Web's four list screens open a form of their own on a route (/notes/new and the rest). The
/// phone has no such route: a list is made here, from one field, so that it exists locally the moment
/// it is named and syncs afterwards - which is the whole point of the offline store. So the field
/// stays, folded away, and the plus is what unfolds it. The screen at rest is then what the web shows -
/// its name and its cards - rather than a box asking to be filled in before anything has been read.
/// </summary>
internal static class NewItemForm
{
	/// <summary>
	/// Folds the form open or shut, and puts the cursor in it when it opens - a field that appears
	/// and then has to be tapped is two presses for one intention.
	/// </summary>
	public static ICommand Toggling(View form, Entry field) => new Command(() =>
	{
		form.IsVisible = !form.IsVisible;

		if (form.IsVisible)
		{
			field.Focus();
		}
	});
}

using Microsoft.Maui.Platform;

namespace Orbit.Maui.Controls;

/// <summary>
/// Puts the on-screen keyboard away, for the panels that cover the page.
///
/// A drawer or a menu can be opened in the middle of typing, and nothing closes the keyboard when it
/// is: the panel then draws itself in the half of the screen the keyboard left, and a list of entries
/// that does not fit in what remains ends up drawn over itself. The press that opens a panel is the
/// moment the reader stopped typing, so the panel is where this belongs.
///
/// Unfocuses the field rather than asking the platform to hide the keyboard and leaving it at that: a
/// field that kept focus behind the panel brings the keyboard straight back when the panel closes.
/// </summary>
internal static class SoftKeyboard
{
	/// <summary>
	/// Drops focus from whatever holds it on the page <paramref name="opener"/> sits on. Does nothing
	/// when nothing is focused, which is the ordinary case - a panel opened without typing first.
	/// </summary>
	public static void Dismiss(Element opener)
	{
		if (PageOf(opener) is not { } page)
		{
			return;
		}

		foreach (var descendant in page.GetVisualTreeDescendants())
		{
			if (descendant is not VisualElement { IsFocused: true } focused)
			{
				continue;
			}

			// Both, and in this order: the explicit ask is what actually lowers the keyboard on Android,
			// and Unfocus is what stops the field asking for it again.
			if (focused is ITextInput input)
			{
				_ = input.HideSoftInputAsync(CancellationToken.None);
			}

			focused.Unfocus();
			return;
		}
	}

	/// <summary>
	/// The page a control is drawn on, walked up the parents rather than read off a window: a panel
	/// being torn down has no window any more, and this is called while one is being opened over a page
	/// that certainly does.
	/// </summary>
	private static Page? PageOf(Element? element)
	{
		while (element is not null and not Page)
		{
			element = element.Parent;
		}

		return element as Page;
	}
}

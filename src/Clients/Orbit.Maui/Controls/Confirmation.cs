namespace Orbit.Maui.Controls;

/// <summary>
/// The question every irreversible press asks first. Orbit.Web asks it with the browser's own
/// <c>confirm</c> - one sentence, two buttons, no title - and the phone's nearest thing is an alert
/// with an empty title, which draws the same shape.
///
/// One helper rather than the same three lines on each screen: what is being deleted differs, and the
/// asking does not.
/// </summary>
internal static class Confirmation
{
	/// <param name="question">The whole sentence, naming the thing - "Delete note "Shopping"?".</param>
	/// <param name="goAhead">What the button that does it says, so the answer is not a bare "OK".</param>
	public static Task<bool> AskAsync(Page page, string question, string goAhead, string cancel)
		=> page.DisplayAlertAsync(string.Empty, question, goAhead, cancel);
}

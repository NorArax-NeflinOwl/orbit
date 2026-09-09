namespace Orbit.Maui.Controls;

/// <summary>
/// The question every irreversible press asks first. Orbit.Web asks it with the browser's own
/// <c>confirm</c>; the phone draws its own - see <see cref="ConfirmationDialog"/>, which is the app's
/// panel rather than Android's alert.
///
/// One helper rather than the same lines on each screen: what is being deleted differs, and the asking
/// does not.
/// </summary>
internal static class Confirmation
{
	/// <param name="question">The whole sentence, naming the thing - "Delete note "Shopping"?".</param>
	/// <param name="goAhead">What the button that does it says, so the answer is not a bare "OK".</param>
	public static async Task<bool> AskAsync(Page page, string question, string goAhead, string cancel)
	{
		// Laid over whatever the page already draws, spanning all of it, exactly as the menu panel and
		// the drawer are - so it needs the page to be built on a Grid, which every screen in the app is.
		// A page that is not gets Android's alert rather than nothing at all: a question that cannot be
		// drawn must still be asked.
		if (page is not ContentPage { Content: Grid root })
		{
			return await page.DisplayAlertAsync(string.Empty, question, goAhead, cancel);
		}

		var dialog = new ConfirmationDialog(question, goAhead, cancel);
		Grid.SetRow(dialog, 0);
		Grid.SetRowSpan(dialog, Math.Max(root.RowDefinitions.Count, 1));
		Grid.SetColumn(dialog, 0);
		Grid.SetColumnSpan(dialog, Math.Max(root.ColumnDefinitions.Count, 1));

		root.Children.Add(dialog);

		try
		{
			return await dialog.Answer;
		}
		finally
		{
			root.Children.Remove(dialog);
		}
	}
}

namespace Orbit.Maui.Controls;

/// <inheritdoc cref="ConfirmationDialog"/>
public partial class ConfirmationDialog : ContentView
{
	private readonly TaskCompletionSource<bool> _answer = new();

	public ConfirmationDialog(string question, string goAhead, string cancel)
	{
		InitializeComponent();

		Question.Text = question;
		GoAheadButton.Text = goAhead;
		CancelButton.Text = cancel;

		// Named for a screen reader here rather than in the markup, because the words are the caller's:
		// what is being deleted differs from screen to screen, and "OK" would tell a reader nothing
		// about which of the two answers they were about to give.
		SemanticProperties.SetDescription(GoAheadButton, goAhead);
		SemanticProperties.SetDescription(CancelButton, cancel);

		GoAheadButton.Clicked += (_, _) => _answer.TrySetResult(true);
		CancelButton.Clicked += (_, _) => _answer.TrySetResult(false);
	}

	/// <summary>
	/// What the reader said. Completed once and once only, so a double press cannot answer twice - and
	/// so <see cref="Dismiss"/> can settle it for a dialog nobody answered.
	/// </summary>
	public Task<bool> Answer => _answer.Task;

	/// <summary>
	/// Answers no on behalf of a reader who never got the chance - a page leaving underneath it, say.
	/// The safe answer for a question whose other button cannot be undone.
	/// </summary>
	public void Dismiss() => _answer.TrySetResult(false);
}

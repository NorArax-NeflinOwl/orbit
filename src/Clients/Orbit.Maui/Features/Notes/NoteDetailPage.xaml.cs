using System.Windows.Input;
using Orbit.Mobile.Localization;
using Orbit.Maui.Controls;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Features.Notes;

/// <summary>
/// One note, written on one surface.
///
/// The screen is the note and nothing else: the name is its first line, the rest is what it says, and
/// everything that can be *done* to it hangs under its name in the bar - how much it matters, whether
/// it is private, who it is shared with, and deleting it. What is left on the page is two buttons over
/// its foot, which is the design's own arrangement.
/// </summary>
public partial class NoteDetailPage : ContentPage, ITitleMenu
{
	private readonly NoteDetailViewModel _viewModel;
	private readonly Translations _translations;

	/// <summary>
	/// The line the caret is in, which is what the tick-box button acts on. Followed here rather than
	/// on the view model: which control has focus is a fact about the screen, and the view model has no
	/// controls.
	/// </summary>
	private NoteLineRow? _beingWrittenIn;

	/// <summary>
	/// A line just started whose field does not exist yet, waiting for one so the caret can be put in
	/// it. Only for the lines where that is actually true: a line started by Enter has its field before
	/// the command that made it has even returned - see PutTheCaretIn, which is where both cases meet.
	/// </summary>
	private NoteLineRow? _toFocus;

	public NoteDetailPage(NoteDetailViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: the menu is bound from the static part of the tree,
		// which is built there and reads a page's plain property exactly once - see
		// CalendarEventDetailPage, where the same order matters for the same reason.
		ShowTitleMenuCommand = new Command(ShowNoteMenu);
		JoinTheLineAboveCommand = new Command<Entry>(JoinTheLineAbove);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = translations;
		ChecklistButton.Command = new Command(PutABoxOnThisLine);
	}

	/// <summary>Typed so the navigator can hand the page its note without casting the binding context.</summary>
	public NoteDetailViewModel ViewModel => _viewModel;

	/// <summary>
	/// The note's own menu, which the bar hangs under its name. One per page, because only one is ever
	/// open and the panel that draws it has to sit above everything else.
	/// </summary>
	public ScreenMenu Menu { get; } = new();

	/// <inheritdoc cref="ITitleMenu.ShowTitleMenuCommand"/>
	public ICommand ShowTitleMenuCommand { get; }

	/// <summary>
	/// What backspace at the head of a line means, handed to every line's field by the template - see
	/// NoteLineKeys. It is bound in the markup rather than attached when the field loads, because the
	/// Android half reads it while the field's handler is being built, which is before Loaded: attached
	/// any later, the key is never listened for at all and backspace silently does nothing.
	///
	/// One command for every line, so it is told which field the press came from.
	/// </summary>
	public ICommand JoinTheLineAboveCommand { get; }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>Lets go of the note's edit lock as the screen leaves - see EditLock.</summary>
	protected override async void OnDisappearing()
	{
		base.OnDisappearing();
		await _viewModel.CloseAsync();
	}

	/// <summary>
	/// Enter at the end of a line starts the next one and puts the caret in it. The new line inherits
	/// the indentation of this one, and starts with a tick box while the button in the corner is on -
	/// see NoteDetailViewModel.AddLineAfter.
	/// </summary>
	private void OnLineCompleted(object? sender, EventArgs eventArgs)
	{
		if (!_viewModel.CanEdit || (sender as Entry)?.BindingContext is not NoteLineRow row)
		{
			return;
		}

		// The caret is asked for here as well as in Loaded, because the field is usually built while
		// AddLineAfter is still running - a BindableLayout answers a row being added straight away -
		// and so it has already loaded by the time there is a row to compare it against. Left to Loaded
		// alone the ask arrived too late every time, and the only thing that moved the caret was
		// Android's own answer to the key, which takes it out of the writing altogether.
		PutTheCaretIn(_viewModel.AddLineAfter(row));
	}

	/// <summary>
	/// Puts the caret in a line, if its field is there to take it - and if it is not, leaves the ask for
	/// <see cref="OnLineLoaded"/> to honour when the field arrives.
	///
	/// Asked for on the next turn of the loop rather than now, because the press that made the line is
	/// still being dealt with: the field says ReturnType="Next", so Android answers the key by moving
	/// focus on to the next control it can find - the button in the corner - and it does that after
	/// this runs. Asked for now, the caret would land in the new line and be taken straight out of it
	/// again, which reads as Enter having done nothing but lose your place.
	/// </summary>
	private void PutTheCaretIn(NoteLineRow? line)
	{
		if (line is null)
		{
			return;
		}

		if (!_fields.TryGetValue(line, out var field))
		{
			_toFocus = line;
			return;
		}

		_toFocus = null;
		Dispatcher.Dispatch(() => field.Focus());
	}

	private void OnLineFocused(object? sender, FocusEventArgs eventArgs)
	{
		if ((sender as Entry)?.BindingContext is NoteLineRow row)
		{
			_beingWrittenIn = row;
		}
	}

	/// <summary>
	/// Where a line whose field did not exist yet gets the caret - a line added from anywhere other than
	/// the key that made it, where the field really is built afterwards.
	/// </summary>
	private void OnLineLoaded(object? sender, EventArgs eventArgs)
	{
		if (sender is not Entry field || field.BindingContext is not NoteLineRow row)
		{
			return;
		}

		_fields[row] = field;

		if (ReferenceEquals(row, _toFocus))
		{
			PutTheCaretIn(row);
		}
	}

	/// <summary>
	/// Which field is drawing which line, so the caret can be put back after two lines are joined - the
	/// field it lands in already exists, so there is no Loaded coming to catch it in.
	///
	/// A BindableLayout builds one field per row and keeps it, rather than recycling as a CollectionView
	/// does, so a row is a stable key. Rebuilt from scratch whenever the note is read back.
	/// </summary>
	private readonly Dictionary<NoteLineRow, Entry> _fields = [];

	/// <summary>
	/// Backspace with the caret at the head of a line: the line joins the one above it and the caret
	/// lands where the two met, which is what a text field does everywhere.
	/// </summary>
	private void JoinTheLineAbove(Entry? field)
	{
		if (!_viewModel.CanEdit || field?.BindingContext is not NoteLineRow row)
		{
			return;
		}

		if (_viewModel.MergeIntoTheLineAbove(row) is not { } landing)
		{
			return;
		}

		_fields.Remove(row);

		if (!_fields.TryGetValue(landing.Line, out var above))
		{
			return;
		}

		above.Focus();
		// Where the two lines met, so carrying on typing carries on where the reader left off rather
		// than at the end of what they have just pulled up.
		above.CursorPosition = Math.Min(landing.Caret, above.Text?.Length ?? 0);
	}

	/// <summary>
	/// The button in the bottom-left corner. It does two things at once because the design gives it
	/// two: it puts a tick box on the line being written in, and it keeps putting one on every new line
	/// until it is pressed again - which also takes the box off whatever line is being written in then.
	/// </summary>
	private void PutABoxOnThisLine()
		=> _viewModel.ToggleChecklistCommand.Execute(_beingWrittenIn ?? _viewModel.Lines.LastOrDefault());

	/// <summary>
	/// What the note can be asked, under its own name in the bar: what it is worth, whether it is
	/// sealed, who else may read it, and getting rid of it. There is no "Back" among them - the phone's
	/// own gesture is the way out of every detail screen.
	/// </summary>
	private void ShowNoteMenu()
	{
		List<ScreenMenuEntry> entries = [];

		if (_viewModel.CanEdit)
		{
			entries.Add(new ScreenMenuEntry(_translations["Priority"], ShowPriorityMenu));
			entries.Add(new ScreenMenuEntry(
				_translations["Private"],
				() => _viewModel.IsPrivate = !_viewModel.IsPrivate,
				_viewModel.IsPrivate));
			// Nothing to show for a private note: the server holds no readable copy to hand anybody,
			// which is what makes it private - see SharePanel.CanShare.
			entries.Add(new ScreenMenuEntry(
				_translations["Share"],
				() => Sharing.IsVisible = !Sharing.IsVisible,
				Sharing.IsVisible,
				canBeChosen: !_viewModel.IsPrivate));
		}

		// Somebody else's note is not this reader's to delete: the same press takes it off their own
		// list and leaves the owner's alone, which is why it is named for what it will actually do.
		entries.Add(new ScreenMenuEntry(
			_viewModel.IsSharedWithMe ? _translations["Remove from my list"] : _translations["Delete note"],
			() => _ = DeleteAsync()));

		// Only once there is one, and here rather than in the account's menu: a history belongs to the
		// thing it is the history of.
		if (_viewModel.HasHistory)
		{
			entries.Add(new ScreenMenuEntry(_translations["History"], () => _viewModel.GoToHistoryCommand.Execute(null)));
		}

		Menu.Show(entries);
	}

	private void ShowPriorityMenu() => Menu.Show(
		_viewModel.Priorities.Select(priority => new ScreenMenuEntry(
			priority.Name,
			() => _viewModel.ChosenPriority = priority,
			priority.Value == _viewModel.ChosenPriority.Value)),
		_translations["Priority"]);

	/// <summary>Asked first, as every delete in Orbit is - and named, so the question says which note.</summary>
	private async Task DeleteAsync()
	{
		var goAhead = _viewModel.IsSharedWithMe
			? _translations["Remove from my list"]
			: _translations["Delete"];
		var question = _viewModel.IsSharedWithMe
			? _translations.Format("Remove \"{0}\" from your list? The owner keeps it.", _viewModel.Title)
			: _translations.Format("Delete note \"{0}\"?", _viewModel.Title);

		if (await Confirmation.AskAsync(this, question, goAhead, _translations["Cancel"]))
		{
			_viewModel.DeleteCommand.Execute(null);
		}
	}
}

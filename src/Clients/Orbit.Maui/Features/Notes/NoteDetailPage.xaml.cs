using System.Windows.Input;
using Orbit.Mobile.Localization;
using Orbit.Maui.Controls;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Notes;

namespace Orbit.Maui.Features.Notes;

public partial class NoteDetailPage : ContentPage, ITitleMenu
{
	private readonly NoteDetailViewModel _viewModel;
	private readonly Translations _translations;

	public NoteDetailPage(NoteDetailViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: the rail's menu is bound from the static part of the
		// tree, which is built there and reads a page's plain property exactly once - see
		// CalendarEventDetailPage, where the same order matters for the same reason.
		ShowLineMenuCommand = new Command<NoteLineRow>(ShowLineMenu);
		ShowTitleMenuCommand = new Command(ShowNoteMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = translations;
	}

	/// <summary>Typed so the navigator can hand the page its note without casting the binding context.</summary>
	public NoteDetailViewModel ViewModel => _viewModel;

	/// <summary>
	/// Whatever menu is open on this screen - a line's, or the note's own from the rail. One per page,
	/// because only one is ever open and the panel that draws it has to sit above everything else.
	/// </summary>
	public ScreenMenu Menu { get; } = new();

	/// <summary>
	/// What a line's "⋯" opens. On the page rather than the view model because which actions a menu
	/// offers is what the screen shows, not what the note knows - the same reason ConversationPage
	/// keeps its message menu here.
	/// </summary>
	public ICommand ShowLineMenuCommand { get; }

	/// <summary>The same, for the note itself: what the rail's "⋯" opens.</summary>
	public ICommand ShowTitleMenuCommand { get; }

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

	private void ShowLineMenu(NoteLineRow? line)
	{
		if (line is null)
		{
			return;
		}

		Menu.Show(
			[
				new ScreenMenuEntry(
					line.IsChecklistItem
						? _translations["Make it an ordinary line"]
						: _translations["Make it a checklist item"],
					() => _viewModel.ToggleChecklistCommand.Execute(line)),
				new ScreenMenuEntry(_translations["Delete line"], () => _viewModel.RemoveLineCommand.Execute(line))
			],
			_translations["Line options"],
			placement: MenuPlacement.FromTheFoot);
	}

	/// <summary>
	/// The note's own actions. They used to be a row of words under the last line, out of reach on a
	/// long note; they hang under the note's own name now, which is in the bar and therefore always
	/// where the reader can see it.
	/// </summary>
	private void ShowNoteMenu()
	{
		// No "Back" among them: the bar's arrow is the way out of every detail screen since the
		// navigation stack landed, and a second one inside the menu is the same duplicate the pages
		// themselves were carrying.
		List<ScreenMenuEntry> entries = [];

		if (_viewModel.CanEdit)
		{
			entries.Add(new ScreenMenuEntry(_translations["Delete note"], () => _viewModel.DeleteCommand.Execute(null)));
		}

		// Only once there is one, and here rather than in the account's menu: a history belongs to the
		// thing it is the history of.
		if (_viewModel.HasHistory)
		{
			entries.Add(new ScreenMenuEntry(_translations["History"], () => _viewModel.GoToHistoryCommand.Execute(null)));
		}

		Menu.Show(entries);
	}
}

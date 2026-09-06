using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Notes;

namespace Orbit.Maui.Features.Notes;

public partial class NotesPage : ContentPage
{
	private readonly NotesViewModel _viewModel;
	private readonly Translations _translations;

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public NotesViewModel ViewModel => _viewModel;

	public NotesPage(NotesViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: a card's menu is bound from a DataTemplate, which is
		// not built until there is a row - but the overlay that draws it is in the static tree, which
		// reads a page's plain property exactly once. See CalendarEventDetailPage.
		_translations = translations;
		ShowCardMenuCommand = new Command<NoteListItem>(ShowCardMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
	}

	/// <summary>What a card's three dots open.</summary>
	public ICommand ShowCardMenuCommand { get; }

	/// <summary>The panel they draw - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>
	/// What a card offers besides opening it. One entry today, and it is named for what it will
	/// actually do: somebody else's note is not this reader's to delete, so pressing it takes the note
	/// off their own list and leaves the owner's alone - which is what Orbit.Web's card says too.
	/// </summary>
	private void ShowCardMenu(NoteListItem? row)
	{
		if (row is not { HasCardMenu: true })
		{
			return;
		}

		Menu.Show(
			[
				new ScreenMenuEntry(
					row.IsSharedWithMe ? _translations["Remove from my list"] : _translations["Delete"],
					() => _ = DeleteAsync(row))
			],
			opensUpwards: true);
	}

	/// <summary>
	/// Asked first, as every delete in Orbit is - and named, so the question says which note and what
	/// will happen to it.
	/// </summary>
	private async Task DeleteAsync(NoteListItem row)
	{
		var goAhead = row.IsSharedWithMe ? _translations["Remove from my list"] : _translations["Delete"];
		var question = row.IsSharedWithMe
			? _translations.Format("Remove \"{0}\" from your list? The owner keeps it.", row.DisplayTitle)
			: _translations.Format("Delete note \"{0}\"?", row.DisplayTitle);

		if (await Confirmation.AskAsync(this, question, goAhead, _translations["Cancel"]))
		{
			_viewModel.DeleteCommand.Execute(row);
		}
	}
}

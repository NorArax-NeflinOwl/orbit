using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Notes;

namespace Orbit.Maui.Features.Notes;

public partial class NotesPage : ContentPage, ITitleMenu
{
	private readonly NotesViewModel _viewModel;
	private readonly Translations _translations;

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public NotesViewModel ViewModel => _viewModel;

	public NotesPage(NotesViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: the menu is bound from the static part of the tree,
		// which is built there and reads a page's plain property exactly once. See
		// CalendarEventDetailPage.
		_translations = translations;
		ShowTitleMenuCommand = new Command(ShowTheListMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
		_nameAFolder = NewItemForm.Toggling(FolderRow, FolderField);
	}

	/// <inheritdoc cref="ITitleMenu.ShowTitleMenuCommand"/>
	public ICommand ShowTitleMenuCommand { get; }

	/// <summary>
	/// Unfolds the row a folder is named in, the same way the plus unfolds the row a note is named in -
	/// see NewItemForm. Chosen from the menu rather than standing on the screen: making a folder is
	/// something somebody does once, and the screen at rest is a column of notes.
	/// </summary>
	private readonly ICommand _nameAFolder;

	/// <summary>The panel it draws - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>
	/// What hangs under the screen's name: how the list is read, in one panel of two named groups.
	///
	/// It used to be two entries that opened the two sets in turn, on the reasoning that eight choices
	/// under two silent headings is a menu nobody reads. The headings are not silent any more - a menu
	/// is groups now, each under its own, which is what the design draws - so the second press is gone
	/// and both halves are on screen at once. The sort heading still says the pins stay on top
	/// whatever is chosen, rather than leaving the reader to notice.
	/// </summary>
	private void ShowTheListMenu() => Menu.ShowGroups(
		[
			// Which folder is being read, with how many notes are in each - the row of tabs the browser
			// draws above its cards, as entries here because a phone has no room for a row of them. The
			// count is the entry's own quiet column, and a folder holding nothing still shows: a tab
			// that appeared only once something was in it could never be filed into.
			new ScreenMenuGroup(
				_translations["Folders"],
				_viewModel.FolderChoices.Select(choice => new ScreenMenuEntry(
					choice.Name,
					() => _viewModel.ChooseFolderCommand.Execute(choice.Key),
					choice.IsChosen,
					count: ScreenMenuEntry.CountOf(choice.Count)))),
			new ScreenMenuGroup(_translations["Folder"], FolderActions()),
			new ScreenMenuGroup(
				_translations["Sort - pinned stay on top"],
				ListMenus.SortOrders(_translations).Select(order => new ScreenMenuEntry(
					order.Name,
					() => Arrange(_viewModel.Arrangement with { SortOrder = order.Value }),
					order.Value == _viewModel.Arrangement.SortOrder))),
			new ScreenMenuGroup(
				_translations["Show"],
				ListMenus.Filters(_translations).Select(filter => new ScreenMenuEntry(
					filter.Name,
					() => Arrange(_viewModel.Arrangement with { Filter = filter.Value }),
					filter.Value == _viewModel.Arrangement.Filter)))
		]);

	/// <summary>
	/// What can be done to the folders themselves. Deleting is offered only while one somebody made is
	/// the one being read - the three built-in folders are not rows and cannot be got rid of, and an
	/// entry that is grey on three tabs out of five is worse than one that is not there.
	/// </summary>
	private List<ScreenMenuEntry> FolderActions()
	{
		List<ScreenMenuEntry> entries =
		[
			new ScreenMenuEntry(_translations["New folder"], () =>
			{
				_viewModel.StartNamingANewFolder();
				_nameAFolder.Execute(null);
			})
		];

		if (_viewModel.Folders.Chosen.FolderId is not null)
		{
			// The same row, opened on the folder's present name - a folder is a name, so changing it is
			// the question "New folder" asks with an answer already in the box. See
			// NotesViewModel.FolderBeingRenamed.
			entries.Add(new ScreenMenuEntry(_translations["Rename folder"], () =>
			{
				_viewModel.StartRenamingTheOpenFolder();
				UnfoldTheFolderRow();
			}));
			// Marked while it is hidden, as the browser's own entry is - the tick says what is true now.
			entries.Add(new ScreenMenuEntry(
				_translations["Hide on the dashboard"],
				() => _viewModel.ToggleShownOnTheDashboardCommand.Execute(null),
				_viewModel.IsChosenFolderHiddenOnTheDashboard));
			entries.Add(new ScreenMenuEntry(_translations["Delete folder"], () => _ = DeleteTheFolderAsync()));
		}

		return entries;
	}

	/// <summary>Opens the folder row if it is shut, and only puts the cursor in it if it is already open.</summary>
	private void UnfoldTheFolderRow()
	{
		if (FolderRow.IsVisible)
		{
			FolderField.Focus();
			return;
		}

		_nameAFolder.Execute(null);
	}

	/// <summary>
	/// Asked first, as every delete in Orbit is - and the question says what it does *not* do, because
	/// "delete folder" reads like the notes go with it and they do not.
	/// </summary>
	private async Task DeleteTheFolderAsync()
	{
		var question = _translations.Format(
			"Delete the folder \"{0}\"? Nothing in it is deleted - it goes back to Public, or to Private if it is sealed.",
			_viewModel.ChosenFolderName);

		if (await Confirmation.AskAsync(this, question, _translations["Delete folder"], _translations["Cancel"]))
		{
			_viewModel.DeleteFolderCommand.Execute(null);
		}
	}

	private void Arrange(ListArrangement arrangement) => _viewModel.ArrangeCommand.Execute(arrangement);
}

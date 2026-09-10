using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Features.Tasks;

public partial class TasksPage : ContentPage, ITitleMenu
{
	private readonly TasksViewModel _viewModel;
	private readonly Translations _translations;

	public TasksPage(TasksViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: it is bound from the static part of the tree, which is
		// built there and reads a page's plain property exactly once - see CalendarEventDetailPage,
		// where the same order matters for the same reason.
		ShowTitleMenuCommand = new Command(ShowTheListMenu);
		ShowCardMenuCommand = new Command<TaskListRow>(ShowCardMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = translations;
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
		_nameAFolder = NewItemForm.Toggling(FolderRow, FolderField);
	}

	/// <summary>
	/// What the screen's name in the top bar opens: how this list of lists is read. It used to be three
	/// dots at the other end of a header that no longer exists - see ITitleMenu.
	/// </summary>
	public ICommand ShowTitleMenuCommand { get; }

	/// <summary>And what a card's own three dots open. The same panel; only the entries differ.</summary>
	public ICommand ShowCardMenuCommand { get; }

	/// <summary>The panel those dots draw - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

	/// <inheritdoc cref="Notes.NotesPage._nameAFolder"/>
	private readonly ICommand _nameAFolder;

	/// <summary>
	/// The row template's pin needs a command that lives on the screen rather than on the row, and a
	/// RelativeSource walks the visual tree - so it names the page and comes through here.
	/// </summary>
	public TasksViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	/// <summary>
	/// What a card offers besides opening it. One entry, and only on a list this reader owns: a shared
	/// one is somebody else's to delete, so its card carries no menu at all rather than a spent one.
	/// </summary>
	private void ShowCardMenu(TaskListRow? row)
	{
		if (row is not { HasCardMenu: true })
		{
			return;
		}

		Menu.Show(
			[new ScreenMenuEntry(_translations["Delete"], () => _ = DeleteAsync(row))],
			placement: MenuPlacement.FromTheFoot);
	}

	/// <summary>
	/// Asked first, as every delete in Orbit is - and named, so the question says which list. What the
	/// browser asks second, about the other lists a group list gathers, is not asked: the phone's own
	/// delete takes one list at a time and cannot carry that answer - see TasksViewModel.
	/// </summary>
	private async Task DeleteAsync(TaskListRow row)
	{
		var question = _translations.Format("Delete task list \"{0}\"?", row.DisplayTitle);
		if (await Confirmation.AskAsync(this, question, _translations["Delete"], _translations["Cancel"]))
		{
			_viewModel.DeleteListCommand.Execute(row);
		}
	}

	/// <summary>
	/// What hangs under the screen's name: how the lists are read - the order, the state, and the
	/// reader's own categories, in one panel of named groups. These were two rows of chips across the
	/// top of the page, and a screen for finding a list opened with a third of itself given over to
	/// settings; then three entries that opened three panels in turn. The design draws one panel, and
	/// a menu is groups now, so that is what this is.
	///
	/// Rebuilt from scratch on every choice that leaves it open, rather than ticking the entry that was
	/// pressed: choosing one rebuilds the categories and moves the tick off whichever entry was
	/// carrying it, and only the choices themselves know which that was.
	/// </summary>
	private void ShowTheListMenu()
	{
		List<ScreenMenuGroup> groups =
		[
			// Which folder is being read, with how many lists are in each - see NotesPage, which draws
			// the same group for the same reason. A finished list nobody filed gathers under Finished;
			// one its owner put in a folder of their own stays there, finished or not.
			new(_translations["Folders"], _viewModel.FolderChoices.Select(choice => new ScreenMenuEntry(
				choice.Name,
				() => _viewModel.ChooseFolderCommand.Execute(choice.Key),
				choice.IsChosen,
				count: ScreenMenuEntry.CountOf(choice.Count)))),

			new(_translations["Folder"], FolderActions()),

			// The one in force is marked, as the dashboard's card filters mark theirs: the menu covers
			// the list it is about, so it has to say for itself which order that list is in.
			new(_translations["Sort"], _viewModel.SortChoices.Select(choice => new ScreenMenuEntry(
				choice.Name,
				() =>
				{
					_viewModel.ChooseSortOrderCommand.Execute(choice);
					ShowTheListMenu();
				},
				choice.IsChosen,
				staysOpen: true))),

			// Where a list stands - what the chips along the top used to say. One choice and then done,
			// unlike the two groups around it: a list is in one state at a time.
			new(_translations["Show"], _viewModel.Filters.Select(filter => new ScreenMenuEntry(
				filter.Name,
				() => _viewModel.FilterByCommand.Execute(filter),
				filter.IsChosen,
				count: ScreenMenuEntry.CountOf(filter.Count))))
		];

		// Only where anything is filed under one. Categories are the reader's own words and most
		// accounts have none, so the group appears when there is something behind it - which is also
		// why Show leaves an empty group out rather than drawing a heading over nothing.
		if (_viewModel.HasCategories)
		{
			// Several at once, so they stay open - and the question of whether an entry needs all of
			// them or any of them is the last row of the same group, asked only once two are chosen:
			// with one, the two questions have the same answer.
			List<ScreenMenuEntry> categories =
			[
				.. _viewModel.Categories.Select(category => new ScreenMenuEntry(
					category.Name,
					() =>
					{
						_viewModel.ToggleCategoryCommand.Execute(category);
						ShowTheListMenu();
					},
					category.IsChosen,
					staysOpen: true,
					count: ScreenMenuEntry.CountOf(category.Count)))
			];

			if (_viewModel.IsCategoryRuleWorthAsking)
			{
				categories.Add(new ScreenMenuEntry(
					_translations["Entries in every chosen category"],
					() =>
					{
						_viewModel.MatchesEveryCategory = !_viewModel.MatchesEveryCategory;
						ShowTheListMenu();
					},
					_viewModel.MatchesEveryCategory,
					staysOpen: true));
			}

			groups.Add(new ScreenMenuGroup(_translations["Categories"], categories));
		}

		Menu.ShowGroups(groups);
	}

	/// <inheritdoc cref="Notes.NotesPage.FolderActions"/>
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
			entries.Add(new ScreenMenuEntry(_translations["Rename folder"], () =>
			{
				_viewModel.StartRenamingTheOpenFolder();
				UnfoldTheFolderRow();
			}));
			entries.Add(new ScreenMenuEntry(_translations["Delete folder"], () => _ = DeleteTheFolderAsync()));
		}

		return entries;
	}

	/// <inheritdoc cref="Notes.NotesPage.UnfoldTheFolderRow"/>
	private void UnfoldTheFolderRow()
	{
		if (FolderRow.IsVisible)
		{
			FolderField.Focus();
			return;
		}

		_nameAFolder.Execute(null);
	}

	/// <inheritdoc cref="Notes.NotesPage.DeleteTheFolderAsync"/>
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
}

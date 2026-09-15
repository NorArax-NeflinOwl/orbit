using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Inventory;

namespace Orbit.Maui.Features.Inventory;

public partial class InventoryPage : ContentPage, ITitleMenu
{
	private readonly InventoryViewModel _viewModel;
	private readonly Translations _translations;

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public InventoryViewModel ViewModel => _viewModel;

	public InventoryPage(InventoryViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: the overlay that draws a card's menu is in the static
		// tree, which reads a page's plain property exactly once - see CalendarEventDetailPage.
		_translations = translations;
		ShowCardMenuCommand = new Command<InventoryRow>(ShowCardMenu);
		ShowTitleMenuCommand = new Command(ShowTheFolderMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
		_nameAFolder = NewItemForm.Toggling(FolderRow, FolderField);
	}

	/// <summary>
	/// What the screen's own name opens - which folder is being read, and what can be done to the
	/// folders themselves. The browser draws them as a row of tabs above the cards; here they are
	/// entries in the menu under the name, which is what the design draws and what the notes and the
	/// task lists already do.
	/// </summary>
	public ICommand ShowTitleMenuCommand { get; }

	/// <summary>
	/// Unfolds the row a folder is named in, the same way the plus unfolds the row a shelf is named in -
	/// see NewItemForm.
	/// </summary>
	private readonly ICommand _nameAFolder;

	private void ShowTheFolderMenu() => Menu.ShowGroups(
		[
			// Which folder is being read, with how many shelves are in each. A folder holding nothing
			// still shows: a tab that appeared only once something was in it could never be filed into.
			new ScreenMenuGroup(
				_translations["Folders"],
				_viewModel.FolderChoices.Select(choice => new ScreenMenuEntry(
					choice.Name,
					() => _viewModel.ChooseFolderCommand.Execute(choice.Key),
					choice.IsChosen,
					count: ScreenMenuEntry.CountOf(choice.Count)))),
			new ScreenMenuGroup(_translations["Folder"], FolderActions())
		]);

	/// <summary>
	/// What can be done to the folders themselves - see NotesPage.FolderActions, which this mirrors.
	/// Deleting and renaming are offered only while one somebody made is the one being read.
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
			entries.Add(new ScreenMenuEntry(_translations["Rename folder"], () =>
			{
				_viewModel.StartRenamingTheOpenFolder();
				UnfoldTheFolderRow();
			}));
			// Marked while it is hidden, as the browser's own entry is - the tick says what is true now.
			// Offered here because the shelves are cards the dashboard is made of.
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
	/// "delete folder" reads like the shelves go with it and they do not.
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
	/// Share and Delete, each left out where it does not apply rather than drawn spent - the same two
	/// Orbit.Web's Inventory card offers, under the same rules: a private inventory is handed to
	/// nobody, and one shared with this reader is not theirs to delete.
	/// </summary>
	private void ShowCardMenu(InventoryRow? row)
	{
		if (row is not { HasCardMenu: true })
		{
			return;
		}

		List<ScreenMenuEntry> entries = [];

		if (row.CanBeShared)
		{
			entries.Add(new ScreenMenuEntry(
				_translations["Share"], () => _viewModel.OfferToShareCommand.Execute(row)));
		}

		if (!row.IsSharedWithMe)
		{
			entries.Add(new ScreenMenuEntry(_translations["Delete"], () => _ = DeleteAsync(row)));
		}

		Menu.Show(entries, placement: MenuPlacement.FromTheFoot);
	}

	/// <summary>
	/// Asked first, as every delete in Orbit is. The question names what goes with it: an inventory is
	/// the shelf and everything on it, which is not obvious from a card showing only a name.
	/// </summary>
	private async Task DeleteAsync(InventoryRow row)
	{
		var question = _translations.Format("Delete \"{0}\" and everything in it?", row.DisplayName);
		if (await Confirmation.AskAsync(this, question, _translations["Delete"], _translations["Cancel"]))
		{
			_viewModel.DeleteCommand.Execute(row);
		}
	}
}

using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Inventory;

namespace Orbit.Maui.Features.Inventory;

public partial class InventoryPage : ContentPage
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

		Menu.Show(entries, opensUpwards: true);
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

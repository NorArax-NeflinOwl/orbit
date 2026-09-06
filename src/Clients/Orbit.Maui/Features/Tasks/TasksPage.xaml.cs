using System.Windows.Input;
using Orbit.Maui.Controls;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Features.Tasks;

public partial class TasksPage : ContentPage
{
	private readonly TasksViewModel _viewModel;
	private readonly Translations _translations;

	public TasksPage(TasksViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: it is bound from the static part of the tree, which is
		// built there and reads a page's plain property exactly once - see CalendarEventDetailPage,
		// where the same order matters for the same reason.
		ShowSortMenuCommand = new Command(ShowSortMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = translations;
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
	}

	/// <summary>What the three dots at the header's other end open.</summary>
	public ICommand ShowSortMenuCommand { get; }

	/// <summary>The panel those dots draw - one per screen, above everything else on it.</summary>
	public ScreenMenu Menu { get; } = new();

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
	/// What order to read the lists in. Orbit's own panel rather than the platform's action sheet, and
	/// under a heading, because that is what Orbit.Web's Tasks header opens - and it stays open while a
	/// reader tries one order and then another, which is the exception its OverflowMenu.StaysOpen makes.
	/// </summary>
	private void ShowSortMenu()
	{
		// The one in force is marked, as the dashboard's card filters mark theirs: the menu covers the
		// list it is about, so it has to say for itself which order that list is in.
		Menu.Show(
			_viewModel.SortChoices.Select(choice => new ScreenMenuEntry(
				choice.Name,
				() =>
				{
					_viewModel.ChooseSortOrderCommand.Execute(choice);

					// Asked again rather than ticked here: the tick has to leave whichever entry was
					// carrying it, and only the choices themselves know which that was.
					ShowSortMenu();
				},
				choice.IsChosen,
				staysOpen: true)),
			_translations["Sort"]);
	}
}

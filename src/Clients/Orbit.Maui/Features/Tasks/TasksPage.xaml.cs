using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Features.Tasks;

public partial class TasksPage : ContentPage
{
	private readonly TasksViewModel _viewModel;
	private readonly Translations _translations;

	public TasksPage(TasksViewModel viewModel, Translations translations)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = translations;
	}

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

	private async void OnSortClicked(object? sender, EventArgs e)
	{
		// The one in force is marked, as the dashboard's card filters mark theirs: the button says what
		// the order is, but once the menu is covering it that answer is off screen.
		var choices = _viewModel.SortChoices;
		var names = choices
			.Select(choice => choice.IsChosen ? $"{choice.Name} ✓" : choice.Name)
			.ToArray();

		var chosen = await DisplayActionSheet(
			_translations["Sort"], _translations["Cancel"], destruction: null, names);

		if (Array.IndexOf(names, chosen) is var picked and >= 0)
		{
			_viewModel.ChooseSortOrderCommand.Execute(choices[picked]);
		}
	}
}

using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Features.Tasks;

public partial class TasksPage : ContentPage
{
	private readonly TasksViewModel _viewModel;

	public TasksPage(TasksViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
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
}

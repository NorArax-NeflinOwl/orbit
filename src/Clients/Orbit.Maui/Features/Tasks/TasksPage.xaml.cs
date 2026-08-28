using Orbit.Mobile.Screens.Tasks;

namespace Orbit.Maui.Features.Tasks;

public partial class TasksPage : ContentPage
{
	private readonly TasksViewModel _viewModel;

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public TasksViewModel ViewModel => _viewModel;

	public TasksPage(TasksViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

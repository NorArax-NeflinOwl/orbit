namespace Orbit.Maui.Features.Tasks;

public partial class TaskListDetailPage : ContentPage
{
	/// <summary>
	/// Typed so the item template's bindings back up to the page can be compiled - see the comment in
	/// the XAML about why they go through the page rather than naming the view model directly.
	/// </summary>
	public TaskListDetailViewModel ViewModel => _viewModel;

	private readonly TaskListDetailViewModel _viewModel;

	public TaskListDetailPage(TaskListDetailViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

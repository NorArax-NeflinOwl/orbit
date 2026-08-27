using Orbit.Mobile.Screens.Dashboard;

namespace Orbit.Maui.Features.Dashboard;

public partial class DashboardPage : ContentPage
{
	private readonly DashboardViewModel _viewModel;

	public DashboardPage(DashboardViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the card rows' bindings back up to the page can be compiled.</summary>
	public DashboardViewModel ViewModel => _viewModel;

	/// <summary>
	/// Reloaded every time. This reads the local store, which every synchroniser writes to behind the
	/// app's back, so coming back to the dashboard is exactly when it is most likely to be stale.
	/// </summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

using Orbit.Mobile.Screens.Inventory;

namespace Orbit.Maui.Features.Inventory;

public partial class WarehouseDetailPage : ContentPage
{
	private readonly WarehouseDetailViewModel _viewModel;

	public WarehouseDetailPage(WarehouseDetailViewModel viewModel)
	{
		InitializeComponent();
		_viewModel = viewModel;
		BindingContext = viewModel;
	}

	/// <summary>
	/// Typed so the item template's bindings back up to the page can be compiled - see the comment in the
	/// XAML about why they go through the page rather than naming the view model directly.
	/// </summary>
	public WarehouseDetailViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

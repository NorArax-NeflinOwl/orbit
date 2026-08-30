using Orbit.Mobile.Screens.Inventory;

namespace Orbit.Maui.Features.Inventory;

public partial class InventoryPage : ContentPage
{
	private readonly InventoryViewModel _viewModel;

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public InventoryViewModel ViewModel => _viewModel;

	public InventoryPage(InventoryViewModel viewModel)
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

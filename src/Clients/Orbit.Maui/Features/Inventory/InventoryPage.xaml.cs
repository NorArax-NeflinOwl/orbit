using System.Windows.Input;
using Orbit.Maui.Controls;
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
		ToggleAddCommand = NewItemForm.Toggling(AddRow, AddField);
	}

	/// <summary>What the plus in the header opens - see NewItemForm.</summary>
	public ICommand ToggleAddCommand { get; }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

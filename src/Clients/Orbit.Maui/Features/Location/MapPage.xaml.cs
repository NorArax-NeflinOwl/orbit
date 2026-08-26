using Orbit.Mobile.Screens.Location;

namespace Orbit.Maui.Features.Location;

public partial class MapPage : ContentPage
{
	private readonly MapViewModel _viewModel;

	public MapPage(MapViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public MapViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

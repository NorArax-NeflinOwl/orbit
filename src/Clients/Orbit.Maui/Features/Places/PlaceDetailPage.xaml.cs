using Orbit.Mobile.Screens.Places;

namespace Orbit.Maui.Features.Places;

/// <summary>
/// One place: a name, a point and three answers about how it is drawn. The whole of a place, which is
/// why there is nothing behind this screen.
/// </summary>
public partial class PlaceDetailPage : ContentPage
{
	public PlaceDetailPage(PlaceDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = ViewModel = viewModel;
	}

	public PlaceDetailViewModel ViewModel { get; }
}

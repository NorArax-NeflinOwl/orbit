using Orbit.Mobile.Screens.Sharing;

namespace Orbit.Maui.Features.Sharing;

public partial class SharedLinkPage : ContentPage
{
	public SharedLinkPage(SharedLinkViewModel viewModel)
	{
		InitializeComponent();
		ViewModel = viewModel;
		BindingContext = viewModel;
	}

	public SharedLinkViewModel ViewModel { get; }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		ViewModel.LoadCommand.Execute(null);
	}
}

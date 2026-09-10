using Orbit.Mobile.Screens.Sharing;

namespace Orbit.Maui.Features.Sharing;

public partial class InvitationPage : ContentPage
{
	public InvitationPage(InvitationViewModel viewModel)
	{
		InitializeComponent();
		ViewModel = viewModel;
		BindingContext = viewModel;
	}

	public InvitationViewModel ViewModel { get; }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		ViewModel.LoadCommand.Execute(null);
	}
}

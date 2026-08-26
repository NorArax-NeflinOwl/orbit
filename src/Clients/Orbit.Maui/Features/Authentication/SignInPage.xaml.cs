using Orbit.Mobile.Screens.Authentication;

namespace Orbit.Maui.Features.Authentication;

public partial class SignInPage : ContentPage
{
	public SignInPage(SignInViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}

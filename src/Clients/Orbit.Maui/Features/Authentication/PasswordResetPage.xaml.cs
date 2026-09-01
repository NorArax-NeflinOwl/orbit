using Orbit.Mobile.Screens.Authentication;

namespace Orbit.Maui.Features.Authentication;

public partial class PasswordResetPage : ContentPage
{
	public PasswordResetPage(PasswordResetViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = viewModel;
	}
}

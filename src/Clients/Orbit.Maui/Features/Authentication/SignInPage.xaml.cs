using Orbit.Mobile.Screens.Authentication;

namespace Orbit.Maui.Features.Authentication;

public partial class SignInPage : ContentPage
{
	private readonly SignInViewModel _viewModel;

	public SignInPage(SignInViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>
	/// Asks the server whether Google sign-in is on offer here. Every time the screen appears rather than
	/// once: the first showing may have had no connection, and this screen is where somebody with no
	/// connection waits.
	/// </summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

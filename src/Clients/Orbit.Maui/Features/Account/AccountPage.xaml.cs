namespace Orbit.Maui.Features.Account;

public partial class AccountPage : ContentPage
{
	private readonly AccountViewModel _viewModel;

	public AccountPage(AccountViewModel viewModel)
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

using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class ContactInfoPage : ContentPage
{
	private readonly ContactInfoViewModel _viewModel;

	public ContactInfoPage(ContactInfoViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	public ContactInfoViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

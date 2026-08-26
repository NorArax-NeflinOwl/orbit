namespace Orbit.Maui.Features.Chat;

public partial class ContactsPage : ContentPage
{
	private readonly ContactsViewModel _viewModel;

	public ContactsPage(ContactsViewModel viewModel)
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

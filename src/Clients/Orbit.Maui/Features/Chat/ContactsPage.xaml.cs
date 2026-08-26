using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class ContactsPage : ContentPage
{
	private readonly ContactsViewModel _viewModel;

	public ContactsPage(ContactsViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>
	/// Typed so the row template's binding back up to the page can be compiled - see the comment in the
	/// XAML about why it goes through the page rather than naming the view model directly.
	/// </summary>
	public ContactsViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

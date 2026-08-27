using Orbit.Mobile.Screens.Notifications;

namespace Orbit.Maui.Features.Notifications;

public partial class NotificationSettingsPage : ContentPage
{
	private readonly NotificationSettingsViewModel _viewModel;

	public NotificationSettingsPage(NotificationSettingsViewModel viewModel)
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

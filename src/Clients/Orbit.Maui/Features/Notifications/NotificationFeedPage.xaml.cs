using Orbit.Mobile.Screens.Notifications;

namespace Orbit.Maui.Features.Notifications;

public partial class NotificationFeedPage : ContentPage
{
	private readonly NotificationFeedViewModel _viewModel;

	public NotificationFeedPage(NotificationFeedViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public NotificationFeedViewModel ViewModel => _viewModel;

	/// <summary>
	/// Reloaded every time rather than once: the feed's whole subject is what happened while the reader
	/// was somewhere else, so coming back to it is exactly when it is most likely to be out of date.
	/// </summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

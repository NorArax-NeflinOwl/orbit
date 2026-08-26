using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class ConversationPage : ContentPage
{
	private readonly ConversationViewModel _viewModel;

	public ConversationPage(ConversationViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the navigator can hand the page its contact without casting the binding context.</summary>
	public ConversationViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
		_viewModel.StartPolling();
	}

	/// <summary>
	/// Polling belongs to the screen, not to the app - a conversation nobody is looking at should cost
	/// nothing. See ConversationViewModel for why this is not the web client's once-a-second loop.
	/// </summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.StopPolling();
	}
}

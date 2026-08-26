namespace Orbit.Maui.Features.Chat;

public partial class GroupConversationPage : ContentPage
{
	private readonly GroupConversationViewModel _viewModel;

	public GroupConversationPage(GroupConversationViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the navigator can hand the page its group without casting the binding context.</summary>
	public GroupConversationViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
		_viewModel.StartPolling();
	}

	/// <summary>
	/// Polling belongs to the screen, not to the app - see GroupConversationViewModel for why this one
	/// ticks more slowly than a one-to-one conversation's.
	/// </summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.StopPolling();
	}
}

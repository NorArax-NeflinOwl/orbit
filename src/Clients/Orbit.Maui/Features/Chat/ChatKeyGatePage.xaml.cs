namespace Orbit.Maui.Features.Chat;

public partial class ChatKeyGatePage : ContentPage
{
	private readonly ChatKeyGateViewModel _viewModel;

	public ChatKeyGatePage(ChatKeyGateViewModel viewModel)
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

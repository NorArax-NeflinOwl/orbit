using Orbit.Mobile.Screens.Chat;

namespace Orbit.Maui.Features.Chat;

public partial class GroupDetailPage : ContentPage
{
	private readonly GroupDetailViewModel _viewModel;

	public GroupDetailPage(GroupDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the member rows' bindings back up to the page can be compiled.</summary>
	public GroupDetailViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

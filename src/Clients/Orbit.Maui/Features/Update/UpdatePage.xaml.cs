using Orbit.Mobile.Screens.Update;

namespace Orbit.Maui.Features.Update;

public partial class UpdatePage : ContentPage
{
	private readonly UpdateViewModel _viewModel;

	public UpdatePage(UpdateViewModel viewModel)
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

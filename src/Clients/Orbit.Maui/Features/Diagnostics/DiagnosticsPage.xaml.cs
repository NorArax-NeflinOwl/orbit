using Orbit.Mobile.Screens.Diagnostics;

namespace Orbit.Maui.Features.Diagnostics;

public partial class DiagnosticsPage : ContentPage
{
	private readonly DiagnosticsViewModel _viewModel;

	public DiagnosticsPage(DiagnosticsViewModel viewModel)
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

using Orbit.Mobile.Screens.Copies;

namespace Orbit.Maui.Features.Copies;

public partial class CopyHistoryPage : ContentPage
{
	private readonly CopyHistoryViewModel _viewModel;

	public CopyHistoryPage(CopyHistoryViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the rows' bindings back up to the page can be compiled.</summary>
	public CopyHistoryViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

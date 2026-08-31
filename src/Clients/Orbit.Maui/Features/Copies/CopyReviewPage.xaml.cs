using Orbit.Mobile.Screens.Copies;

namespace Orbit.Maui.Features.Copies;

public partial class CopyReviewPage : ContentPage
{
	private readonly CopyReviewViewModel _viewModel;

	public CopyReviewPage(CopyReviewViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the cards' bindings back up to the page can be compiled.</summary>
	public CopyReviewViewModel ViewModel => _viewModel;

	/// <summary>
	/// Read again every time: a review is about what changed elsewhere, so the answer is only good for
	/// as long as it takes the next pull to arrive.
	/// </summary>
	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

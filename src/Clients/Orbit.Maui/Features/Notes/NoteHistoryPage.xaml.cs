using Orbit.Mobile.Screens.Notes;

namespace Orbit.Maui.Features.Notes;

public partial class NoteHistoryPage : ContentPage
{
	private readonly NoteHistoryViewModel _viewModel;

	public NoteHistoryPage(NoteHistoryViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the rows' bindings back up to the page can be compiled.</summary>
	public NoteHistoryViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

using Orbit.Mobile.Screens.Notes;

namespace Orbit.Maui.Features.Notes;

public partial class NoteDetailPage : ContentPage
{
	private readonly NoteDetailViewModel _viewModel;

	public NoteDetailPage(NoteDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the navigator can hand the page its note without casting the binding context.</summary>
	public NoteDetailViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

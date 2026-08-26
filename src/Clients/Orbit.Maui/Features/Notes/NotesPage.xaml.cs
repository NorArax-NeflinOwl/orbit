namespace Orbit.Maui.Features.Notes;

public partial class NotesPage : ContentPage
{
	private readonly NotesViewModel _viewModel;

	public NotesPage(NotesViewModel viewModel)
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

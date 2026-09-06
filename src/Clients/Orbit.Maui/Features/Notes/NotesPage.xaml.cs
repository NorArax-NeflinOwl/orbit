using Orbit.Maui.Controls;
using Orbit.Mobile.Screens.Notes;

namespace Orbit.Maui.Features.Notes;

public partial class NotesPage : ContentPage
{
	private readonly NotesViewModel _viewModel;

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public NotesViewModel ViewModel => _viewModel;

	public NotesPage(NotesViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		AddButton.Command = NewItemForm.Toggling(AddRow, AddField);
	}

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

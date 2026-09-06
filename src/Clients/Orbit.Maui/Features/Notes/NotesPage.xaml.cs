using System.Windows.Input;
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
		ToggleAddCommand = NewItemForm.Toggling(AddRow, AddField);
	}

	/// <summary>What the plus in the header opens - see NewItemForm.</summary>
	public ICommand ToggleAddCommand { get; }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

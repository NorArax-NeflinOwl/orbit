using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Maui.Features.Calendar;

public partial class CalendarPage : ContentPage
{
	private readonly CalendarViewModel _viewModel;

	public CalendarPage(CalendarViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>
	/// The grid's day cells need a command that lives on the screen rather than on the cell, and a
	/// RelativeSource walks the visual tree - so it names the page and comes through here.
	/// </summary>
	public CalendarViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

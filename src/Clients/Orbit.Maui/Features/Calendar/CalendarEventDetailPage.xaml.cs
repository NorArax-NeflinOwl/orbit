using Orbit.Mobile.Screens.Calendar;

namespace Orbit.Maui.Features.Calendar;

public partial class CalendarEventDetailPage : ContentPage
{
	private readonly CalendarEventDetailViewModel _viewModel;

	public CalendarEventDetailPage(CalendarEventDetailViewModel viewModel)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>Typed so the navigator can hand the page its event without casting the binding context.</summary>
	public CalendarEventDetailViewModel ViewModel => _viewModel;

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

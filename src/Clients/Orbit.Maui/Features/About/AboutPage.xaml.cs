using System.Windows.Input;
using Orbit.Mobile.Screens.About;

namespace Orbit.Maui.Features.About;

/// <summary>
/// What Orbit is, which build this one is, and where its documents are - see <see cref="AboutViewModel"/>.
/// </summary>
public partial class AboutPage : ContentPage
{
	private readonly AboutViewModel _viewModel;

	public AboutPage(AboutViewModel viewModel)
	{
		// Before InitializeComponent, not after: the rows' command is bound from a DataTemplate against
		// a plain page property, which is read exactly once - see CalendarEventDetailPage.
		OpenDocumentCommand = new Command<AboutDocument>(Open);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
	}

	/// <summary>
	/// Opens one of the documents in whatever the phone reads pages with. A page command rather than a
	/// view model one, because launching a URL is the platform's job - the same reason the drawer's
	/// licence link was a handler.
	/// </summary>
	public ICommand OpenDocumentCommand { get; }

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}

	private static async void Open(AboutDocument? document)
	{
		if (document is not null)
		{
			await Launcher.Default.OpenAsync(document.Url);
		}
	}
}

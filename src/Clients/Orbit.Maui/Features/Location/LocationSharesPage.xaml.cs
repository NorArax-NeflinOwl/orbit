using Microsoft.Maui.Maps;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Screens.Location;
using MapPoint = Orbit.Mobile.Screens.Location.MapPoint;
using PhoneMaps = Microsoft.Maui.ApplicationModel.Map;

namespace Orbit.Maui.Features.Location;

/// <summary>
/// One of the map's two lists of people: who can see where the reader is, or who is letting the reader
/// see where they are.
///
/// A screen rather than a panel under the map, which is where both used to live. The map takes the whole
/// screen now, and a list of names drawn over it covers the thing it is about.
///
/// Both read <see cref="MapViewModel"/>, which loads both either way - so this page is the same page
/// twice, told by the navigator which of its two lists to show.
/// </summary>
public partial class LocationSharesPage : ContentPage
{
	private readonly MapViewModel _viewModel;
	private readonly Translations _translations;

	public LocationSharesPage(MapViewModel viewModel, Translations translations)
	{
		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_translations = translations;
	}

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public MapViewModel ViewModel => _viewModel;

	/// <summary>
	/// Which of the two lists this is. Told before the screen appears rather than chosen here, the same
	/// way every other screen that takes an argument is - see AppNavigator.
	/// </summary>
	public void Show(bool theirs)
	{
		Mine.IsVisible = !theirs;
		Theirs.IsVisible = theirs;
		Title = theirs ? _translations["Shared with you"] : _translations["Who can see you"];
	}

	/// <inheritdoc cref="MapPage.OpenInMapsCommand"/>
	public Command<ReceivedPosition> OpenInMapsCommand => new(async received =>
	{
		if (_viewModel.WhereToOpen(received) is { } destination)
		{
			await OpenInPhoneMapsAsync(destination);
		}
	});

	private static Task OpenInPhoneMapsAsync(MapPoint destination)
		=> PhoneMaps.Default.OpenAsync(
			destination.Latitude, destination.Longitude, new MapLaunchOptions { Name = destination.Label });

	protected override void OnAppearing()
	{
		base.OnAppearing();
		_viewModel.LoadCommand.Execute(null);
	}
}

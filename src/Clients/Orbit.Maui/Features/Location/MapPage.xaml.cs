using System.Collections.Specialized;
using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Orbit.Maui.Platform;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Screens.Location;
using MapPoint = Orbit.Mobile.Screens.Location.MapPoint;
using PhoneMaps = Microsoft.Maui.ApplicationModel.Map;
using SensorLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace Orbit.Maui.Features.Location;

public partial class MapPage : ContentPage, Orbit.Maui.Controls.ITitleMenu
{
	/// <summary>
	/// How much ground the map shows when it first has something to point at. Wide enough to place a
	/// position in its neighbourhood, close enough that a street is still legible.
	/// </summary>
	private static readonly Distance InitialRadius = Distance.FromKilometers(2);

	private readonly MapViewModel _viewModel;

	/// <summary>Set once the map has been pointed somewhere, so later readings do not yank it back.</summary>
	private bool _hasBeenCentred;

	/// <summary>False when this build cannot show one, which makes every pin below pointless.</summary>
	private bool _hasMap = true;

	private readonly Translations _translations;

	public MapPage(MapViewModel viewModel, Translations translations)
	{
		// Before InitializeComponent, not after: the menu is bound from the static part of the tree,
		// which is built there and reads a page's plain property exactly once - see CalendarEventDetailPage.
		_translations = translations;
		ShowTitleMenuCommand = new Command(ShowTheMapMenu);

		InitializeComponent();
		BindingContext = _viewModel = viewModel;
		_viewModel.Points.CollectionChanged += OnPointsChanged;

		if (!MapAvailability.CanShowMap)
		{
			SayThereIsNoMap(translations);
		}
	}

	/// <inheritdoc cref="Orbit.Maui.Controls.ITitleMenu.ShowTitleMenuCommand"/>
	public System.Windows.Input.ICommand ShowTitleMenuCommand { get; }

	/// <summary>The panel it draws - one per screen, above everything else on it.</summary>
	public Orbit.Mobile.Screens.ScreenMenu Menu { get; } = new();

	/// <summary>
	/// What can be done about where the reader is, and who else is on this map. All of it was a column
	/// of panels under a map a sixth of the screen tall; the map takes the screen now and this is where
	/// the panels went.
	///
	/// The two lists open as screens of their own rather than unfolding here: a list of people is a list
	/// of people, and drawing one over a map means covering the thing it is about.
	/// </summary>
	private void ShowTheMapMenu()
	{
		List<Orbit.Mobile.Screens.ScreenMenuGroup> groups =
		[
			// Two ways to share, as Orbit.Web offers: the point read a moment ago, or that point and
			// every one after it while this screen is open. A phone is the thing that moves, so the
			// second is the one worth having here. Neither means anything until a position has been read.
			//
			// The design offers a third, "Off", above these two. There is nothing here for it to mean:
			// sharing is not a state this screen is in but a set of people it is shared with, and
			// stopping is per person, on the list screen - which is where the design puts it too.
			new(_translations["Sharing"],
			[
				new(_translations["Send once"],
					() => _viewModel.ShareOnceCommand.Execute(null),
					canBeChosen: _viewModel.HasOwnPosition),
				new(_translations["Keep sharing"],
					() => _viewModel.KeepSharingCommand.Execute(null),
					canBeChosen: _viewModel.HasOwnPosition)
			]),

			// How many, where there are any: a standing "0" beside a list nobody is on is not news, the
			// same rule the dashboard's chat-request counter follows.
			new(_translations["Locations"],
			[
				new(_translations["Who can see you"],
					() => _viewModel.OpenSharingWithCommand.Execute(null),
					count: Orbit.Mobile.Screens.ScreenMenuEntry.CountOf(_viewModel.SharingWith.Count)),
				new(_translations["Shared with you"],
					() => _viewModel.OpenSharedWithMeCommand.Execute(null),
					count: Orbit.Mobile.Screens.ScreenMenuEntry.CountOf(_viewModel.SharedWithMe.Count))
			])
		];

		// Hidden unless the account qualifies - see GoogleIntegrationAccess. Orbit.Web turns the address
		// itself into this link; here it is an entry, because there is no address written on the screen.
		// Under no heading of its own: it belongs to neither of the two above, and one loose action at
		// the foot of a menu is what a row's menu looks like anyway.
		if (_viewModel.CanOpenOwnPositionInGoogleMaps)
		{
			groups.Add(new Orbit.Mobile.Screens.ScreenMenuGroup(
				null,
				[
					new(_translations["Open in Google Maps"], () => _ = OpenInGoogleMapsAsync())
				]));
		}

		Menu.ShowGroups(groups);
	}

	/// <summary>
	/// Takes the map out of the page before anything renders it, which is the only moment that helps:
	/// on Android a map built without a key throws from inside Play Services and ends the process. The
	/// rest of the screen - reading a position, sharing it, and everyone sharing with the reader - does
	/// not involve the map and keeps working, so the page loses a picture rather than its purpose.
	/// </summary>
	private void SayThereIsNoMap(Translations translations)
	{
		_hasMap = false;
		MapArea.Content = new Label
		{
			// Says what still works, not only what does not: every position somebody has shared can be
			// opened in the phone's own map app from the list below, map or no map.
			Text = translations["The map can't be shown in this build. A shared position still opens in your phone's map app."],
			FontSize = 12,
			VerticalOptions = LayoutOptions.Center,
			HorizontalTextAlignment = TextAlignment.Center,
			LineBreakMode = LineBreakMode.WordWrap
		};
	}

	/// <summary>Typed so the list rows' bindings back up to the page can be compiled.</summary>
	public MapViewModel ViewModel => _viewModel;

	/// <summary>
	/// Opens a position somebody shared in whatever this phone treats as Maps - what the button on each
	/// "Shared with you" row does, and what tapping the pin's own callout does.
	///
	/// On the page rather than on the view model because leaving the app is a platform call, the same
	/// split "Open in Google Maps" already draws: the view model answers *where* (see WhereToOpen), this
	/// answers *how*. And the phone's own map app rather than a Google Maps URL, because this is the one
	/// thing here that has to work when Orbit cannot draw a map itself.
	/// </summary>
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

		// A live share keeps going only while somebody is looking at this screen - see StartRefreshing.
		_viewModel.StartRefreshing();
	}

	/// <summary>
	/// The page outlives none of this, but the view model is resolved per screen and the handler would
	/// otherwise keep this page alive behind it.
	/// </summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_viewModel.StopRefreshing();
		_viewModel.Points.CollectionChanged -= OnPointsChanged;
	}

	/// <summary>
	/// Leaving the app is a platform call, so the page makes it. The view model built the URL, which is
	/// the half worth testing - the same split as the action sheets on the detail screens.
	/// </summary>
	private async Task OpenInGoogleMapsAsync()
	{
		if (_viewModel.OwnPositionInGoogleMapsUrl is { } url)
		{
			await Launcher.Default.OpenAsync(url);
		}
	}

	private void OnPointsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ShowPins();

	private void ShowPins()
	{
		if (!_hasMap)
		{
			return;
		}

		PositionsMap.Pins.Clear();
		foreach (var point in _viewModel.Points)
		{
			var pin = new Pin
			{
				Label = point.Label,
				Address = point.Description,
				Location = new SensorLocation(point.Latitude, point.Longitude)
			};
			// Tapping the callout a pin opens hands the point to the phone's map app, which is where
			// somebody who has just found a friend's position wants to be: Orbit draws where it is, and
			// the map app is what knows how to get there.
			pin.InfoWindowClicked += async (_, _) => await OpenInPhoneMapsAsync(point);
			PositionsMap.Pins.Add(pin);
		}

		// Centred on the first point, which is the reader's own whenever they have one - see
		// MapViewModel.ShowPointsOnMap for the order.
		if (!_hasBeenCentred && _viewModel.Points.FirstOrDefault() is { } first)
		{
			PositionsMap.MoveToRegion(MapSpan.FromCenterAndRadius(
				new SensorLocation(first.Latitude, first.Longitude), InitialRadius));
			_hasBeenCentred = true;
		}
	}
}

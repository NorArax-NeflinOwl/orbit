using Microsoft.Maui.Controls.Maps;
using Microsoft.Maui.Maps;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using SensorLocation = Microsoft.Maui.Devices.Sensors.Location;

namespace Orbit.Maui.Features.Location;

/// <summary>
/// The map somebody points at instead of typing an address, opened over the screen that asked - see
/// <see cref="IPlacePicker"/> and Orbit.Web's LocationPickerOverlay, which this mirrors.
///
/// It answers once, through <see cref="Picked"/>: either the address of a confirmed pin, or a
/// cancellation. Backing out with the system gesture counts as cancelling, so the screen that opened it
/// is never left waiting.
/// </summary>
public partial class PlacePickerPage : ContentPage
{
	/// <summary>
	/// How much ground the map shows when it opens. The same radius the positions map uses: wide enough
	/// to place somewhere in its neighbourhood, close enough that a street is still legible.
	/// </summary>
	private static readonly Distance InitialRadius = Distance.FromKilometers(2);

	private readonly Translations _translations;
	private readonly PlaceSearch _placeSearch;
	private readonly TaskCompletionSource<PickedPlace> _answer = new();

	/// <summary>What the last tap turned out to be, and what Use would write back.</summary>
	private string _address = string.Empty;

	/// <summary>Where the pin is, which the event needs - an address alone cannot be put on a map.</summary>
	private SensorLocation? _pin;

	public PlacePickerPage(Translations translations, PlaceSearch placeSearch)
	{
		InitializeComponent();
		_translations = translations;
		_placeSearch = placeSearch;
	}

	/// <summary>Completes when the reader confirms a pin or backs out, whichever happens first.</summary>
	public Task<PickedPlace> Picked => _answer.Task;

	/// <summary>
	/// Opens where the reader was already talking about, when the box held somewhere that can be found.
	/// Best-effort: an address nobody can geocode leaves the map where it was rather than failing.
	/// </summary>
	public async Task StartAtAsync(string address)
	{
		if (address.Trim().Length == 0)
		{
			return;
		}

		try
		{
			var found = (await Geocoding.Default.GetLocationsAsync(address)).FirstOrDefault();
			if (found is not null)
			{
				PlaceMap.MoveToRegion(MapSpan.FromCenterAndRadius(found, InitialRadius));
			}
		}
		catch (Exception exception) when (exception is FeatureNotSupportedException or HttpRequestException)
		{
			// Geocoding needs a network round trip and a platform that offers it. Neither is worth
			// refusing to show a map over.
		}
	}

	/// <summary>Backing out with the system gesture is an answer too, and it is "no".</summary>
	protected override void OnDisappearing()
	{
		base.OnDisappearing();
		_answer.TrySetResult(PickedPlace.Cancelled);
	}

	private async void OnMapClicked(object? sender, MapClickedEventArgs e) => await DropPinAtAsync(e.Location);

	private async void OnSearchRequested(object? sender, EventArgs e) => await SearchAsync(SearchBox.Text ?? string.Empty);

	/// <summary>
	/// Every match is offered rather than the best one: street names repeat, and taking the first would
	/// drop a pin in whichever town Nominatim happened to rank first. A single match is taken straight
	/// away - there is nothing to choose between.
	/// </summary>
	private async Task SearchAsync(string address)
	{
		if (address.Trim().Length == 0)
		{
			return;
		}

		SearchResults.Clear();
		SearchResults.IsVisible = false;
		SearchStatusLabel.IsVisible = true;
		SearchStatusLabel.Text = _translations["Searching…"];

		var matches = await _placeSearch.SearchAsync(address);
		if (matches.Count == 0)
		{
			SearchStatusLabel.Text =
				_translations["Nothing found for that. Try fewer words, or point at it on the map."];
			return;
		}

		SearchStatusLabel.IsVisible = false;
		if (matches is [{ } only])
		{
			await ShowMatchAsync(only);
			return;
		}

		SearchResults.IsVisible = true;
		foreach (var match in matches)
		{
			SearchResults.Add(RowFor(match));
		}
	}

	private Button RowFor(FoundPlace match)
	{
		var row = new Button
		{
			Text = match.Name,
			FontSize = 13,
			Padding = new Thickness(0, 4),
			HorizontalOptions = LayoutOptions.Start,
			LineBreakMode = LineBreakMode.TailTruncation,
			Style = Application.Current?.Resources["LinkButton"] as Style
		};

		row.Clicked += async (_, _) => await ShowMatchAsync(match);
		return row;
	}

	/// <summary>
	/// A picked match moves the pin and asks the same question a tapped one does, so there is one way to
	/// save and not two.
	/// </summary>
	private async Task ShowMatchAsync(FoundPlace match)
	{
		SearchResults.Clear();
		SearchResults.IsVisible = false;

		var location = new SensorLocation(match.Latitude, match.Longitude);
		PlaceMap.MoveToRegion(MapSpan.FromCenterAndRadius(location, InitialRadius));
		await DropPinAtAsync(location, match.Name);
	}

	/// <param name="named">
	/// What the place is already known to be called, for a pin that came from a search - looking it up
	/// again would ask Nominatim what it just said.
	/// </param>
	private async Task DropPinAtAsync(SensorLocation location, string? named = null)
	{
		PlaceMap.Pins.Clear();
		PlaceMap.Pins.Add(new Pin
		{
			Label = _translations["Pick a place"],
			Location = location,
			Type = PinType.Place
		});

		_pin = location;
		PlaceLabel.IsVisible = true;
		PlaceLabel.Text = _translations["Looking that place up…"];
		UseButton.IsEnabled = false;

		_address = named is { Length: > 0 } ? named : await DescribeAsync(location);
		PlaceLabel.Text = _address.Length > 0
			? $"{_translations["Use this place?"]}\n{_address}"
			: _translations["That place has no address, so only the pin says where it is."];

		// A pin nowhere in particular has nothing to write into the box, so there is nothing to confirm.
		UseButton.IsEnabled = _address.Length > 0;
	}

	/// <summary>
	/// The pin in words. Best-effort by design, exactly as PhoneLocation's is: it needs a network round
	/// trip, and a point with no address is still a point somebody chose.
	/// </summary>
	private static async Task<string> DescribeAsync(SensorLocation location)
	{
		try
		{
			var placemark = (await Geocoding.Default.GetPlacemarksAsync(location)).FirstOrDefault();
			if (placemark is null)
			{
				return string.Empty;
			}

			var parts = new[] { placemark.Thoroughfare, placemark.SubThoroughfare, placemark.Locality }
				.Where(part => !string.IsNullOrWhiteSpace(part));

			return string.Join(" ", parts).Trim();
		}
		catch (Exception exception) when (exception is FeatureNotSupportedException or HttpRequestException)
		{
			return string.Empty;
		}
	}

	private async void OnUseClicked(object? sender, EventArgs e)
	{
		if (_pin is { } pin)
		{
			_answer.TrySetResult(PickedPlace.Chosen(_address, pin.Latitude, pin.Longitude));
		}
		await Navigation.PopModalAsync();
	}

	private async void OnCancelClicked(object? sender, EventArgs e)
	{
		_answer.TrySetResult(PickedPlace.Cancelled);
		await Navigation.PopModalAsync();
	}
}

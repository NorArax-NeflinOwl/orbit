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
	private readonly TaskCompletionSource<PickedPlace> _answer = new();

	/// <summary>What the last tap turned out to be, and what Use would write back.</summary>
	private string _address = string.Empty;

	public PlacePickerPage(Translations translations)
	{
		InitializeComponent();
		_translations = translations;
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

	private async Task DropPinAtAsync(SensorLocation location)
	{
		PlaceMap.Pins.Clear();
		PlaceMap.Pins.Add(new Pin
		{
			Label = _translations["Pick a place"],
			Location = location,
			Type = PinType.Place
		});

		PlaceLabel.IsVisible = true;
		PlaceLabel.Text = _translations["Looking that place up…"];
		UseButton.IsEnabled = false;

		_address = await DescribeAsync(location);
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
		_answer.TrySetResult(PickedPlace.Chosen(_address));
		await Navigation.PopModalAsync();
	}

	private async void OnCancelClicked(object? sender, EventArgs e)
	{
		_answer.TrySetResult(PickedPlace.Cancelled);
		await Navigation.PopModalAsync();
	}
}

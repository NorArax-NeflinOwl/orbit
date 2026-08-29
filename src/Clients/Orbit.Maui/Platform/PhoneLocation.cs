using Orbit.Mobile.Location;
using DeviceLocationResult = Orbit.Mobile.Location.DeviceLocationResult;

namespace Orbit.Maui.Platform;

/// <summary>
/// Reads the phone's position, asking for permission the first time.
///
/// "When in use" rather than "always": Orbit only ever wants a position while somebody is looking at
/// the map or has just chosen to share one. Asking for background access would be asking for far more
/// than the feature needs, and iOS shows the reader exactly which one was requested.
/// </summary>
public sealed class PhoneLocation : IDeviceLocation
{
	/// <summary>
	/// Balanced rather than Best: a street-level fix is what a shared position needs, and the best
	/// setting keeps the GPS awake noticeably longer for metres nobody will read off a map.
	/// </summary>
	private static readonly GeolocationRequest Request =
		new(GeolocationAccuracy.Medium, TimeSpan.FromSeconds(15));

	public async Task<DeviceLocationResult> ReadAsync(CancellationToken cancellationToken = default)
	{
		try
		{
			var permission = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();
			if (permission != PermissionStatus.Granted)
			{
				permission = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
			}

			if (permission != PermissionStatus.Granted)
			{
				return new DeviceLocationResult(DeviceLocationOutcome.NotPermitted);
			}

			var location = await Geolocation.Default.GetLocationAsync(Request, cancellationToken);
			if (location is null)
			{
				return new DeviceLocationResult(DeviceLocationOutcome.Unavailable);
			}

			return new DeviceLocationResult(
				DeviceLocationOutcome.Found, location.Latitude, location.Longitude,
				await DescribeAsync(location, cancellationToken));
		}
		catch (Exception exception) when (exception is FeatureNotSupportedException or FeatureNotEnabledException)
		{
			// No location hardware, or it is switched off for the whole device - the reader has to go to
			// Settings either way, which is what "not permitted" tells them.
			return new DeviceLocationResult(DeviceLocationOutcome.NotPermitted);
		}
		catch (PermissionException)
		{
			return new DeviceLocationResult(DeviceLocationOutcome.NotPermitted);
		}
	}

	/// <summary>
	/// A street address for the point, when one can be had. Best-effort by design: it needs a network
	/// round trip, and a position with no label is still a position - failing the whole reading because
	/// the geocoder was unreachable would be the wrong trade.
	/// </summary>
	private static async Task<string?> DescribeAsync(Microsoft.Maui.Devices.Sensors.Location location, CancellationToken cancellationToken)
	{
		try
		{
			var places = await Geocoding.Default.GetPlacemarksAsync(location);
			cancellationToken.ThrowIfCancellationRequested();

			if (places.FirstOrDefault() is not { } place)
			{
				return null;
			}

			var parts = new[] { place.Thoroughfare, place.Locality, place.CountryName }
				.Where(part => !string.IsNullOrWhiteSpace(part));

			return string.Join(", ", parts) is { Length: > 0 } address ? address : null;
		}
		catch (Exception exception) when (exception is not OperationCanceledException)
		{
			return null;
		}
	}
}

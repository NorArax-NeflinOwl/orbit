using Orbit.Mobile.Location;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>A phone that is wherever the test says, or that refuses to say - which no test can arrange for real.</summary>
internal sealed class FixedDeviceLocation : IDeviceLocation
{
    public DeviceLocationResult Reading { get; set; } =
        new(DeviceLocationOutcome.Found, 52.2297, 21.0122, "Marszałkowska, Warszawa, Poland");

    public Task<DeviceLocationResult> ReadAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Reading);
}

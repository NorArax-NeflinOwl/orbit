using Orbit.Mobile.Security;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A phone that answers however the test needs it to, and counts how often it was asked. Stands in for
/// the one thing no test can supply - a face.
/// </summary>
internal sealed class FixedDeviceAuthentication : IDeviceAuthentication
{
    public DeviceAuthenticationOutcome Outcome { get; set; } = DeviceAuthenticationOutcome.Confirmed;

    public int TimesAsked { get; private set; }

    public Task<DeviceAuthenticationOutcome> ConfirmAsync(string reason, CancellationToken cancellationToken = default)
    {
        TimesAsked++;
        return Task.FromResult(Outcome);
    }
}

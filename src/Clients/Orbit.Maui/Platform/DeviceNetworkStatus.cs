using Orbit.Mobile.Sync;

namespace Orbit.Maui.Platform;

/// <summary>
/// What MAUI's connectivity check believes. Only ever used to decide what to offer the user - see
/// <see cref="INetworkStatus"/> for why it is not treated as a guarantee.
/// </summary>
public sealed class DeviceNetworkStatus : INetworkStatus
{
	private readonly IConnectivity _connectivity;

	public DeviceNetworkStatus(IConnectivity connectivity) => _connectivity = connectivity;

	public bool IsOnline => _connectivity.NetworkAccess is NetworkAccess.Internet;
}

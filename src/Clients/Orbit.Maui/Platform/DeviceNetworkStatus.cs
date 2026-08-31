using Orbit.Mobile.Sync;

namespace Orbit.Maui.Platform;

/// <summary>
/// What MAUI's connectivity check believes. Only ever used to decide what to offer the user - see
/// <see cref="INetworkStatus"/> for why it is not treated as a guarantee.
/// </summary>
public sealed class DeviceNetworkStatus : INetworkStatus
{
	private readonly IConnectivity _connectivity;

	public DeviceNetworkStatus(IConnectivity connectivity)
	{
		_connectivity = connectivity;
		// Subscribed for the life of the app rather than per screen: this is a singleton, and a phone
		// changes network far less often than it changes screen.
		_connectivity.ConnectivityChanged += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
	}

	public bool IsOnline => _connectivity.NetworkAccess is NetworkAccess.Internet;

	public event EventHandler? Changed;
}

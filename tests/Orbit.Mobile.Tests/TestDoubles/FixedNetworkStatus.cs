using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>A phone that is simply online, or simply not - and can be made to change its mind.</summary>
internal sealed class FixedNetworkStatus : INetworkStatus
{
    public FixedNetworkStatus(bool isOnline) => IsOnline = isOnline;

    /// <summary>
    /// New instances rather than shared ones: a test that changes its mind must not change it for every
    /// other test in the run, which is what a static singleton would do the moment this stopped being a
    /// record of one bool.
    /// </summary>
    public static FixedNetworkStatus Online => new(true);

    public static FixedNetworkStatus Offline => new(false);

    public bool IsOnline { get; private set; }

    public event EventHandler? Changed;

    /// <summary>Moves the phone on or off the network, telling whoever is listening - see INetworkStatus.</summary>
    public void Becomes(bool isOnline)
    {
        if (IsOnline == isOnline)
        {
            return;
        }

        IsOnline = isOnline;
        Changed?.Invoke(this, EventArgs.Empty);
    }
}

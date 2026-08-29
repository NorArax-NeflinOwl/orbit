using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>A phone that is simply online, or simply not.</summary>
internal sealed record FixedNetworkStatus(bool IsOnline) : INetworkStatus
{
    public static FixedNetworkStatus Online { get; } = new(true);

    public static FixedNetworkStatus Offline { get; } = new(false);
}

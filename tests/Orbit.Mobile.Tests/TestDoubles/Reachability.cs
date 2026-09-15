using Microsoft.Extensions.Logging.Abstractions;
using Orbit.Mobile.Sync;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// A <see cref="ServerReachability"/> for a test that only needs the phone to be on a network or off it.
/// Most screens take the reachability through <see cref="SyncState"/> without caring about a pause;
/// the tests that are about a pause build their own with a notice they can change.
/// </summary>
internal static class Reachability
{
    public static ServerReachability Online => Over(FixedNetworkStatus.Online);

    public static ServerReachability Offline => Over(FixedNetworkStatus.Offline);

    /// <summary>Over the given network, with a deployment that never says it is paused.</summary>
    public static ServerReachability Over(INetworkStatus network, TimeProvider? timeProvider = null)
        => Over(network, new FixedPauseNotice(), timeProvider);

    public static ServerReachability Over(INetworkStatus network, IPauseNoticeReader pauseNotice, TimeProvider? timeProvider = null)
        => new(network, pauseNotice, timeProvider ?? TimeProvider.System, NullLogger<ServerReachability>.Instance);
}

/// <summary>A deployment whose pause notice a test writes and removes.</summary>
internal sealed class FixedPauseNotice : IPauseNoticeReader
{
    /// <summary>What the next read finds - null for no notice, which is how it starts.</summary>
    public PauseNotice? Notice { get; set; }

    public int Reads { get; private set; }

    public Task<PauseNotice?> ReadAsync(CancellationToken cancellationToken)
    {
        Reads++;
        return Task.FromResult(Notice);
    }
}

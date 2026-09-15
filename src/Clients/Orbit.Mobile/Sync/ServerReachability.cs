using Microsoft.Extensions.Logging;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Whether Orbit can be reached - which is what every screen actually wants to know when it asks
/// <see cref="INetworkStatus"/>, and more than the phone's connectivity can tell it.
///
/// A phone on a working network whose server has been stopped is <i>not</i> online in any sense a
/// screen cares about: what it can offer is what it can do alone, exactly as with no signal. So this is
/// the <see cref="INetworkStatus"/> the app registers, and it says online only when the device has a
/// network <b>and</b> the deployment has not said it is paused. Everything that greys out, refuses an
/// offline edit or offers to reconnect follows from that one answer without knowing why.
///
/// How it learns of a pause: <see cref="OrbitAnswerHandler"/> reports every answer that was not Orbit's,
/// and on such an answer this asks the pause notice - once, then no more often than
/// <see cref="RecheckInterval"/> while paused, so a paused day is not a request per sync. Orbit answering
/// as itself ends the pause on the spot, whatever the notice says: the server is the authority on
/// whether the server is up. See info/orbit-maui-plan.md, "Living without the server".
/// </summary>
public sealed class ServerReachability : INetworkStatus
{
    /// <summary>
    /// How long a pause is believed before the notice is read again. Long enough that a stopped server
    /// costs the phone a request every so often rather than one per failed call; short enough that a
    /// deployment resumed without the API being asked first is noticed within the hour.
    /// </summary>
    public static readonly TimeSpan RecheckInterval = TimeSpan.FromMinutes(10);

    private readonly INetworkStatus _device;
    private readonly IPauseNoticeReader _pauseNotice;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<ServerReachability> _logger;
    private readonly SemaphoreSlim _readLock = new(1, 1);

    private DateTimeOffset? _lastReadAtUtc;

    public ServerReachability(
        INetworkStatus device, IPauseNoticeReader pauseNotice, TimeProvider timeProvider,
        ILogger<ServerReachability> logger)
    {
        _device = device;
        _pauseNotice = pauseNotice;
        _timeProvider = timeProvider;
        _logger = logger;
        _device.Changed += (_, _) => Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc/>
    public event EventHandler? Changed;

    /// <summary>A network, and no pause - see the class summary.</summary>
    public bool IsOnline => _device.IsOnline && !IsPaused;

    /// <summary>Whether the phone has a network at all, whatever the deployment says - what the corner needs to tell "no connection" from "paused".</summary>
    public bool HasNetwork => _device.IsOnline;

    /// <summary>True while the deployment has said it is paused and Orbit has not answered since.</summary>
    public bool IsPaused => Notice is not null;

    /// <summary>The notice as last read, or null when not paused.</summary>
    public PauseNotice? Notice { get; private set; }

    /// <summary>Orbit answered as itself, so whatever pause was believed is over.</summary>
    public void RecordAnswerFromOrbit()
    {
        if (Notice is null)
        {
            return;
        }

        _logger.LogInformation("Orbit answered; the pause is over");
        Notice = null;
        _lastReadAtUtc = null;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Something other than Orbit answered at Orbit's address, so find out whether that is the
    /// deployment being paused on purpose. Awaited by the handler before it throws, so that whoever
    /// catches the failure already sees the pause when it reads <see cref="IsPaused"/>.
    /// </summary>
    public async Task RecordAnswerNotFromOrbitAsync(CancellationToken cancellationToken)
    {
        await _readLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsDueForAReading())
            {
                return;
            }

            _lastReadAtUtc = _timeProvider.GetUtcNow();
            var notice = await _pauseNotice.ReadAsync(cancellationToken);
            if (notice == Notice)
            {
                return;
            }

            _logger.LogInformation(
                notice is null ? "Not Orbit answering, and no pause notice" : "The deployment is paused: {Message}",
                notice?.Message);
            Notice = notice;
        }
        finally
        {
            _readLock.Release();
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private bool IsDueForAReading()
        => _lastReadAtUtc is not { } lastRead || _timeProvider.GetUtcNow() - lastRead >= RecheckInterval;
}

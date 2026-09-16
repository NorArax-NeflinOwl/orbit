using Microsoft.Extensions.Logging;
using Orbit.Mobile.Authentication;

namespace Orbit.Mobile.Sync;

/// <summary>
/// Keeps the phone in step with the server while the app is open, without waiting to be asked.
///
/// Every screen synchronises the feature it shows when it is opened, which is enough for somebody moving
/// about the app and not enough for anybody else: a phone left on the dashboard, or on a list nobody
/// navigated away from, went on showing what it had been shown at whatever moment that screen was last
/// opened. What was written in a browser reached it when somebody happened to visit that section again,
/// which is days rather than minutes.
///
/// Started and stopped with the window, beside the presence heartbeat and the live connection - see
/// App.CreateWindow. A phone in somebody's pocket syncs nothing: the work belongs to the app being in
/// front of a reader, and a timer running behind a locked screen is one Android puts to sleep anyway.
/// </summary>
public sealed class PeriodicSync : IDisposable
{
    /// <summary>
    /// Long enough to cost little on a phone's battery and its data, short enough that somebody writing
    /// in a browser and then picking their phone up finds it there. The live connection carries chat and
    /// the notifications already (see ILiveUpdates), so what waits for this tick is the slower half -
    /// notes, lists, appointments, shelves, places - which nobody expects to the second.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    /// <summary>
    /// What one run is, handed over rather than held: <see cref="EverythingSynchronizer"/> is built per
    /// use - each run wants its own database context - and one kept here would outlive every screen that
    /// shares its store. See MauiProgram, which is where the two are put together.
    /// </summary>
    private readonly Func<CancellationToken, Task<SyncResult>> _synchronise;

    private readonly SessionStore _sessionStore;
    private readonly INetworkStatus _networkStatus;
    private readonly SyncState _syncState;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PeriodicSync> _logger;

    private CancellationTokenSource? _running;

    public PeriodicSync(
        Func<CancellationToken, Task<SyncResult>> synchronise, SessionStore sessionStore,
        INetworkStatus networkStatus, SyncState syncState, TimeProvider timeProvider,
        ILogger<PeriodicSync> logger)
    {
        _synchronise = synchronise;
        _sessionStore = sessionStore;
        _networkStatus = networkStatus;
        _syncState = syncState;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>
    /// Starts, or leaves what is already running alone. Called when the app comes to the front and when
    /// somebody signs in, so calling it twice is not two timers - the same rule PresenceReporter follows.
    ///
    /// The first run happens at once rather than one interval later: coming back to the app after an
    /// hour away is exactly the moment the screen on display is most out of date.
    /// </summary>
    public void Start()
    {
        if (_running is not null)
        {
            return;
        }

        _running = new CancellationTokenSource();
        _ = RunAsync(_running.Token);
    }

    public void Stop()
    {
        _running?.Cancel();
        _running?.Dispose();
        _running = null;
    }

    public void Dispose() => Stop();

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(Interval, _timeProvider);
        try
        {
            do
            {
                await SynchroniseAsync(cancellationToken);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // The app went to the background mid-run. This is started without being awaited, so it must
            // not escape.
        }
    }

    /// <summary>
    /// One run. Nothing is attempted with nobody signed in, and nothing while Orbit cannot be reached -
    /// a run then would put "couldn't sync" in the corner every few minutes for a reader who is working
    /// offline on purpose, which is the app behaving as designed.
    ///
    /// "Cannot be reached" is more than having no signal: a deployment somebody stopped on purpose
    /// answers that way too, and there is nothing at the other end of a working network that will answer
    /// as Orbit (see ServerReachability, which is the INetworkStatus the app registers). Nothing is lost
    /// by leaving off - the pause ends when Orbit answers as itself again, and the presence heartbeat
    /// asks far more often than this does.
    ///
    /// The indicator is moved the way a screen's own sync moves it, so the corner tells the same story
    /// whoever asked. The screens are told only when the run actually brought something down: see
    /// SyncState.BroughtSomethingNew.
    /// </summary>
    public async Task SynchroniseAsync(CancellationToken cancellationToken = default)
    {
        if (!_networkStatus.IsOnline || await _sessionStore.GetAsync() is null)
        {
            return;
        }

        _syncState.RecordStarted();
        try
        {
            var result = await _synchronise(cancellationToken);
            if (result.ReachedTheServer)
            {
                _syncState.RecordSucceeded();
            }
            else
            {
                _syncState.RecordFailed();
            }

            if (result.Sent + result.Received + result.RemovedLocally > 0)
            {
                _syncState.RecordBroughtSomethingNew();
            }
        }
        catch (HttpRequestException exception)
        {
            // The server was reached and refused - an expired session, most often. AppNavigator watches
            // the session store and moves to sign-in when that is what happened, so there is nothing to
            // do here beyond not claiming the phone is offline.
            _syncState.RecordFailed();
            _logger.LogInformation("A background sync was refused ({Reason})", exception.Message);
        }
    }
}

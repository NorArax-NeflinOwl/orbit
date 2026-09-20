using Microsoft.Extensions.Logging;
using Orbit.Core.Users;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;

namespace Orbit.Mobile.Presence;

/// <summary>
/// Tells the server this reader is here, and what they chose to be.
///
/// <see cref="Presence"/> answers the same question for the phone itself and always did; this is the
/// half that was missing while the server had no notion of presence at all. Now it does, so the dot a
/// contact sees beside this account's name comes from here - and from here stopping.
///
/// Going quiet is the mechanism rather than a failure: the server ages a silent account from available
/// to away to offline on its own (see <see cref="UserPresence"/>), so the app backgrounding and simply
/// not sending is exactly how somebody who put their phone down stops being shown as present.
/// </summary>
public sealed class PresenceReporter : IDisposable
{
    /// <summary>
    /// Comfortably shorter than <see cref="UserPresence.AwayAfter"/>, so somebody holding the phone
    /// never flickers to away between two heartbeats.
    /// </summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    /// <summary>
    /// The live connection, when there is one. A heartbeat over a connection already open costs a frame;
    /// as a request of its own it costs a handshake and a round trip every twenty seconds - which is the
    /// whole reason the hub takes presence at all. Falls back to the request when there is no connection.
    /// </summary>
    private readonly Live.ILiveUpdates _liveUpdates;

    private readonly Presence _presence;
    private readonly UsersClient _usersClient;
    private readonly SessionStore _sessionStore;
    private readonly ILogger<PresenceReporter> _logger;

    /// <summary>Drives the heartbeat, so a test can let twenty seconds pass without waiting for them.</summary>
    private readonly TimeProvider _timeProvider;

    private CancellationTokenSource? _beating;

    /// <summary>
    /// Set when a choice could not be got through, and cleared once one is. Without it a "do not
    /// disturb" refused by a moment's bad signal was lost for good: the phone kept showing it, the
    /// server never heard of it, and everybody else went on seeing this account as available. The
    /// heartbeat is already a tick, so it carries the retry rather than a timer of its own.
    /// </summary>
    private bool _serverHasNotBeenTold;

    public PresenceReporter(
        Presence presence, UsersClient usersClient, SessionStore sessionStore, ILogger<PresenceReporter> logger,
        TimeProvider timeProvider, Live.ILiveUpdates liveUpdates)
    {
        _liveUpdates = liveUpdates;
        _presence = presence;
        _usersClient = usersClient;
        _sessionStore = sessionStore;
        _logger = logger;
        _timeProvider = timeProvider;
        _presence.ChosenChanged += OnChosenChanged;
    }

    /// <summary>
    /// Starts reporting, or leaves the one already running alone. Called both when the app opens on a
    /// signed-in session and when somebody signs in afterwards, so calling it twice is not two
    /// heartbeats.
    /// </summary>
    public void Start()
    {
        if (_beating is not null)
        {
            return;
        }

        _beating = new CancellationTokenSource();
        _ = BeatAsync(_beating.Token);
    }

    /// <summary>Stops, which is how this account fades out - see the class comment.</summary>
    public void Stop()
    {
        _beating?.Cancel();
        _beating?.Dispose();
        _beating = null;
    }

    public void Dispose()
    {
        _presence.ChosenChanged -= OnChosenChanged;
        Stop();
    }

    private void OnChosenChanged(object? sender, EventArgs e) => _ = ReportChoiceAsync();

    private async Task ReportChoiceAsync()
    {
        if (await _sessionStore.GetAsync() is null)
        {
            return;
        }

        var availability = _presence.Chosen == ChosenAvailability.Unavailable
            ? PresenceAvailability.DoNotDisturb
            : PresenceAvailability.Available;

        try
        {
            // A refusal counts as not told, the same as no connection at all: what matters here is
            // whether the server now knows, not why it does not.
            _serverHasNotBeenTold = !await _usersClient.SetAvailabilityAsync(availability.ToString());
        }
        catch (HttpRequestException exception)
        {
            _serverHasNotBeenTold = true;
            _logger.LogInformation("Could not report the chosen availability ({Reason})", exception.Message);
        }
    }

    private async Task BeatAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval, _timeProvider);

        // Whether the last beat failed, so an outage is said once rather than every twenty seconds.
        var isOutOfTouch = false;
        try
        {
            do
            {
                if (await _sessionStore.GetAsync() is not null)
                {
                    try
                    {
                        // The choice first, when one is still owed: a heartbeat says this account is
                        // here, and saying so while the server still believes it is available is the
                        // state this is meant to get out of.
                        if (_serverHasNotBeenTold)
                        {
                            await ReportChoiceAsync();
                        }

                        // Over the live connection when there is one, which is the cheaper half of what
                        // that connection is for - and as the request it always was when there is not.
                        if (!await _liveUpdates.TryReportPresenceAsync(isAtTheKeyboard: true))
                        {
                            await _usersClient.SendPresenceHeartbeatAsync(cancellationToken);
                        }

                        isOutOfTouch = false;
                    }
                    catch (HttpRequestException exception)
                    {
                        // One failed beat is a silence, and a silence is what this reports anyway - the
                        // server ages a quiet account out on its own.
                        //
                        // The loop carries on, because the signal coming back is the common case. This
                        // used to end the run instead, and there was no way back into it: Start returns
                        // early while a run is recorded, and nothing cleared that record - so one
                        // failed request left the account silent for the rest of the session and the
                        // server showed somebody using the app as away (reported 2026-09-20).
                        if (!isOutOfTouch)
                        {
                            _logger.LogInformation(
                                "Presence heartbeat could not be sent ({Reason})", exception.Message);
                            isOutOfTouch = true;
                        }
                    }
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
        }
    }
}

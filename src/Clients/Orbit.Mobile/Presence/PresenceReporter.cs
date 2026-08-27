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

    private readonly Presence _presence;
    private readonly UsersClient _usersClient;
    private readonly SessionStore _sessionStore;
    private readonly ILogger<PresenceReporter> _logger;

    private CancellationTokenSource? _beating;

    public PresenceReporter(
        Presence presence, UsersClient usersClient, SessionStore sessionStore, ILogger<PresenceReporter> logger)
    {
        _presence = presence;
        _usersClient = usersClient;
        _sessionStore = sessionStore;
        _logger = logger;
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
            await _usersClient.SetAvailabilityAsync(availability.ToString());
        }
        catch (HttpRequestException exception)
        {
            // Not worth telling anybody: the choice is already kept on the phone, and the next
            // heartbeat carries no availability anyway - the reader can set it again if it mattered.
            _logger.LogInformation("Could not report the chosen availability ({Reason})", exception.Message);
        }
    }

    private async Task BeatAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(HeartbeatInterval);
        try
        {
            do
            {
                if (await _sessionStore.GetAsync() is not null)
                {
                    await _usersClient.SendPresenceHeartbeatAsync(cancellationToken);
                }
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
        }
        catch (HttpRequestException exception)
        {
            // One failed heartbeat is a silence, and a silence is already what this reports. Stopping
            // here rather than retrying keeps a phone with no signal from a request every twenty
            // seconds; the next Start - a resume, a sign-in - picks it up again.
            _logger.LogInformation("Presence heartbeat stopped ({Reason})", exception.Message);
        }
    }
}

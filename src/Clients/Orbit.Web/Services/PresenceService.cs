using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;
using Orbit.Core.Users;

namespace Orbit.Web.Services;

/// <summary>
/// Keeps the server told that this person is here, and holds what they chose to be so the avatar can
/// show it. The heartbeat stops while the tab is in the background: presence is meant to say whether
/// somebody is there to answer, and a tab left open behind thirty others is not an answer. Going quiet
/// is therefore the mechanism, not a failure - the server ages a silent account from available to away
/// to offline on its own (see Orbit.Core.Users.UserPresence).
/// </summary>
public sealed class PresenceService(
    UsersApiClient usersApiClient,
    LiveUpdatesConnection liveUpdates,
    IJSRuntime jsRuntime,
    ILogger<PresenceService> logger) : IDisposable
{
    /// <summary>
    /// Comfortably shorter than UserPresence.AwayAfter, so somebody sitting at the page never flickers
    /// to away between two heartbeats.
    /// </summary>
    private static readonly TimeSpan HeartbeatInterval = TimeSpan.FromSeconds(20);

    private readonly CancellationTokenSource _cancellation = new();
    private PeriodicTimer? _timer;
    private bool _isStarted;

    public PresenceAvailability Availability { get; private set; } = PresenceAvailability.Available;

    /// <summary>Raised when the chosen availability changes, so the layout showing it can re-render.</summary>
    public event Action? Changed;

    /// <summary>
    /// Reads back what this account already chose - a person who set "do not disturb" yesterday should
    /// find it still set today - then starts the heartbeat. Called both when the app opens on an
    /// already-signed-in session and when somebody signs in afterwards, so calling it twice leaves the
    /// one heartbeat running rather than adding a second.
    /// </summary>
    public async Task StartAsync()
    {
        if (_isStarted)
        {
            return;
        }

        _isStarted = true;
        try
        {
            var account = await usersApiClient.GetAccountAsync();
            if (account is not null && Enum.TryParse<PresenceAvailability>(account.Availability, out var availability))
            {
                Availability = availability;
                Changed?.Invoke();
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not read the current availability; assuming available");
        }

        _ = RunHeartbeatAsync();
    }

    /// <summary>
    /// Answers whether the choice actually took, so whoever offered it can say when it did not. It used
    /// to answer nothing: a refused request left the picker showing the old choice with no word about
    /// it, and somebody who had just clicked "do not disturb" carried on being told they were available.
    /// A connection that is simply down is the same answer rather than an exception thrown out of a
    /// click handler, which in WebAssembly reaches the console and nobody else.
    /// </summary>
    public async Task<bool> SetAvailabilityAsync(PresenceAvailability availability)
    {
        try
        {
            if (!await usersApiClient.SetAvailabilityAsync(availability.ToString()))
            {
                logger.LogWarning("The server refused the availability change to {Availability}", availability);
                return false;
            }
        }
        catch (HttpRequestException exception)
        {
            logger.LogWarning(exception, "Could not reach the server to change availability to {Availability}", availability);
            return false;
        }

        Availability = availability;
        Changed?.Invoke();
        return true;
    }

    private async Task RunHeartbeatAsync()
    {
        _timer = new PeriodicTimer(HeartbeatInterval);
        await SendHeartbeatIfVisibleAsync();
        try
        {
            while (await _timer.WaitForNextTickAsync(_cancellation.Token))
            {
                await SendHeartbeatIfVisibleAsync();
            }
        }
        catch (OperationCanceledException)
        {
            // The tab is going away, which is exactly what the server should conclude from the silence.
        }
    }

    private async Task SendHeartbeatIfVisibleAsync()
    {
        try
        {
            await using var module = await jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/presence.js");
            if (!await module.InvokeAsync<bool>("isPageVisible"))
            {
                return;
            }

            // Over the live connection when there is one: a frame on a socket that is already open,
            // instead of a request that carries headers, a token and a TLS handshake three times a
            // minute. The HTTP call is what happens when there is no connection, which is also what
            // happened before there was one - so nothing about presence behaves differently, it just
            // costs less when it can.
            if (liveUpdates.IsConnected)
            {
                await liveUpdates.ReportPresenceAsync(true);
                return;
            }

            await usersApiClient.SendPresenceHeartbeatAsync(_cancellation.Token);
        }
        catch (HttpRequestException)
        {
            // Transient: the next tick tries again, and until then the account ages exactly as it should.
        }
        catch (JSDisconnectedException)
        {
            // The circuit is gone; nothing left to report presence for.
        }
    }

    public void Dispose()
    {
        _cancellation.Cancel();
        _timer?.Dispose();
        _cancellation.Dispose();
    }
}

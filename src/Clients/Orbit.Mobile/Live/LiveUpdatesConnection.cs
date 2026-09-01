using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Orbit.Core.LiveUpdates;
using Orbit.Mobile.Authentication;

namespace Orbit.Mobile.Live;

/// <summary>
/// The one connection the app holds open to hear that something changed - the phone's half of what
/// Orbit.Web's LiveUpdatesConnection does, speaking the same hub and the same message names.
///
/// <b>Only while the app is in front.</b> It is started and stopped with the window, like
/// <see cref="Presence.PresenceReporter"/>: a socket held open behind a locked screen is a socket
/// Android will drop in Doze anyway, and what it would have carried is exactly what push already
/// delivers (see PhonePushNotifications). So this speeds up the app somebody is looking at, and push
/// covers the app they are not.
///
/// One for the whole app rather than one per screen: a connection costs a handshake and a token, and
/// opening a second when somebody walks from the chat to the dashboard would pay it again for the same
/// news.
/// </summary>
public sealed class LiveUpdatesConnection : ILiveUpdates, IAsyncDisposable
{
    private readonly SessionStore _sessionStore;
    private readonly TokenRefreshService _tokenRefresh;
    private readonly ILogger<LiveUpdatesConnection> _logger;
    private readonly string _hubUrl;

    private HubConnection? _connection;

    public LiveUpdatesConnection(
        SessionStore sessionStore, TokenRefreshService tokenRefresh, Uri apiBaseAddress,
        ILogger<LiveUpdatesConnection> logger)
    {
        _sessionStore = sessionStore;
        _tokenRefresh = tokenRefresh;
        _logger = logger;
        _hubUrl = new Uri(apiBaseAddress, LiveUpdateMessages.Path.TrimStart('/')).ToString();
    }

    /// <inheritdoc/>
    public event Action? ChatChanged;

    /// <inheritdoc/>
    public event Action? NotificationsChanged;

    /// <inheritdoc/>
    public event Action? ConnectionStateChanged;

    /// <inheritdoc/>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Opens it, or leaves the one already open alone. Called both when the app comes to the front on a
    /// signed-in session and when somebody signs in, so calling it twice is not two connections.
    ///
    /// Does nothing when nobody is signed in: the hub refuses an unauthenticated handshake, and trying
    /// anyway would retry forever behind the sign-in screen.
    /// </summary>
    public async Task StartAsync()
    {
        if (_connection is not null || await _sessionStore.GetAsync() is null)
        {
            return;
        }

        _connection = Build();
        Listen(_connection);

        try
        {
            await _connection.StartAsync();
        }
        catch (Exception exception)
        {
            // Never fatal. Everything this speeds up still works by asking, so a hub that cannot be
            // reached costs latency and nothing else - and there is nothing here a reader could act on.
            _logger.LogInformation(exception, "No live connection; the app keeps asking instead");
        }

        ConnectionStateChanged?.Invoke();
    }

    /// <summary>
    /// Closes it - when the app goes into the background, and on sign-out so the next account does not
    /// inherit this one's connection.
    /// </summary>
    public async Task StopAsync()
    {
        if (_connection is null)
        {
            return;
        }

        var connection = _connection;
        _connection = null;

        try
        {
            await connection.DisposeAsync();
        }
        catch (Exception exception)
        {
            // Closing a connection that is already gone is not a failure worth reporting anywhere.
            _logger.LogDebug(exception, "The live connection did not close cleanly");
        }

        ConnectionStateChanged?.Invoke();
    }

    /// <inheritdoc/>
    public async Task<bool> TryReportPresenceAsync(bool isAtTheKeyboard)
    {
        if (_connection is not { State: HubConnectionState.Connected } connection)
        {
            return false;
        }

        try
        {
            await connection.InvokeAsync(LiveUpdateMessages.ReportPresence, isAtTheKeyboard);
            return true;
        }
        catch (Exception exception)
        {
            // It dropped mid-send. Presence ages on silence by design, and the caller falls back to the
            // request it always made - see PresenceReporter.
            _logger.LogDebug(exception, "Could not report presence over the live connection");
            return false;
        }
    }

    private HubConnection Build()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options => options.AccessTokenProvider = ProvideAccessTokenAsync)
            // Keeps trying rather than giving up after the default four attempts: a phone that spent an
            // hour in a tunnel should come back to a working app, not to one that quietly went on
            // polling until it was restarted.
            .WithAutomaticReconnect(new KeepTryingRetryPolicy())
            .Build();

        connection.Reconnected += _ =>
        {
            ConnectionStateChanged?.Invoke();
            // Whatever happened while it was down was announced to nobody, so the first thing to do on
            // coming back is to assume everything did.
            ChatChanged?.Invoke();
            NotificationsChanged?.Invoke();
            return Task.CompletedTask;
        };

        connection.Reconnecting += _ =>
        {
            ConnectionStateChanged?.Invoke();
            return Task.CompletedTask;
        };

        connection.Closed += _ =>
        {
            ConnectionStateChanged?.Invoke();
            return Task.CompletedTask;
        };

        return connection;
    }

    /// <summary>
    /// PresenceChanged is deliberately not listened for: the phone does not show anybody else's
    /// presence yet, so there would be nothing to redraw. The server sends it to whoever can see it and
    /// an unhandled message costs nothing - see Orbit.Core's LiveUpdateMessages.
    /// </summary>
    private void Listen(HubConnection connection)
    {
        connection.On(LiveUpdateMessages.ChatChanged, () => ChatChanged?.Invoke());
        connection.On(LiveUpdateMessages.NotificationsChanged, () => NotificationsChanged?.Invoke());
    }

    /// <summary>
    /// The token the handshake carries. Asked again on every reconnect, which is what makes an expired
    /// one recoverable: a phone that slept past the access token's lifetime refreshes here rather than
    /// reconnecting forever with a credential the server keeps refusing.
    /// </summary>
    private async Task<string?> ProvideAccessTokenAsync()
    {
        if (await _sessionStore.GetAsync() is { AccessToken: { Length: > 0 } token })
        {
            return token;
        }

        return await _tokenRefresh.TryRefreshAsync(CancellationToken.None)
            ? (await _sessionStore.GetAsync())?.AccessToken
            : null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    /// <summary>
    /// Backs off to half a minute and stays there for as long as the app is open. The built-in policy
    /// gives up after about thirty seconds, which is the wrong answer on a phone: the thing it is
    /// waiting for - a train leaving a tunnel, a server finishing a deploy - routinely takes longer, and
    /// giving up means the app is slower for the rest of the session with nothing on screen to say why.
    /// </summary>
    private sealed class KeepTryingRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) => retryContext.PreviousRetryCount switch
        {
            0 => TimeSpan.Zero,
            1 => TimeSpan.FromSeconds(2),
            2 => TimeSpan.FromSeconds(5),
            3 => TimeSpan.FromSeconds(10),
            _ => TimeSpan.FromSeconds(30)
        };
    }
}

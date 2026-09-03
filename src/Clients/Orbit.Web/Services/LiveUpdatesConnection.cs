using Microsoft.AspNetCore.SignalR.Client;
using Orbit.Core.LiveUpdates;

namespace Orbit.Web.Services;

/// <summary>
/// The one connection this tab holds open to hear that something changed, so the pages showing chat,
/// notifications and presence can stop asking four times a second.
///
/// One for the whole app rather than one per page: a connection costs a handshake and a token, and
/// opening a second one when somebody navigates from the chat to the dashboard would pay it again for
/// the same news. Pages come and go around it and subscribe while they are here.
///
/// It carries no data. Every announcement means "read again", and the page answers it with the same API
/// call its timer used to make - which is what keeps end-to-end encrypted messages on exactly the path
/// they were already on, and keeps this class ignorant of what any of it says.
/// </summary>
public sealed class LiveUpdatesConnection : IAsyncDisposable
{
    private readonly TokenStore _tokenStore;
    private readonly TokenRefreshService _tokenRefreshService;
    private readonly ILogger<LiveUpdatesConnection> _logger;
    private readonly string _hubUrl;

    private HubConnection? _connection;

    public LiveUpdatesConnection(
        TokenStore tokenStore,
        TokenRefreshService tokenRefreshService,
        string apiBaseAddress,
        ILogger<LiveUpdatesConnection> logger)
    {
        _tokenStore = tokenStore;
        _tokenRefreshService = tokenRefreshService;
        _logger = logger;
        _hubUrl = new Uri(new Uri(apiBaseAddress), LiveUpdateMessages.Path.TrimStart('/')).ToString();
    }

    /// <summary>Something in this account's chat changed - a message, a receipt, or an approval.</summary>
    public event Action? ChatChanged;

    /// <summary>Something arrived in, or left, this account's notification feed.</summary>
    public event Action? NotificationsChanged;

    /// <summary>Somebody this account can see came back or changed what they are showing as.</summary>
    public event Action<Guid>? PresenceChanged;

    /// <summary>Raised when the connection comes up or goes down, so a page can put its own timer back to a poll.</summary>
    public event Action? ConnectionStateChanged;

    /// <summary>
    /// Whether announcements are actually arriving. Pages read this to decide how often to fall back to
    /// asking: it is deliberately not assumed. A connection that never came up - an old proxy, a network
    /// that blocks WebSockets, a server that has not been restarted with the hub - has to leave the app
    /// working exactly as it did before rather than silently stopping.
    /// </summary>
    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    /// <summary>
    /// Opens the connection, or does nothing if it is already open. Safe to call on every sign-in and
    /// on an app that started already signed in, the same way PresenceService.StartAsync is.
    /// </summary>
    public async Task StartAsync()
    {
        if (_connection is not null)
        {
            return;
        }

        _connection = BuildConnection();
        SubscribeToAnnouncements(_connection);

        try
        {
            await _connection.StartAsync();
        }
        catch (Exception exception)
        {
            // Never fatal. Everything this connection speeds up still works by asking, so a hub that
            // cannot be reached costs latency and nothing else - and saying so out loud here would be
            // an error message about a feature the reader cannot act on.
            _logger.LogInformation(exception, "No live connection; falling back to polling");
        }

        ConnectionStateChanged?.Invoke();
    }

    /// <summary>Closes it - on sign-out, so the next account does not inherit this one's connection.</summary>
    public async Task StopAsync()
    {
        if (_connection is null)
        {
            return;
        }

        var connection = _connection;
        _connection = null;
        await connection.DisposeAsync();
        ConnectionStateChanged?.Invoke();
    }

    /// <summary>
    /// Says somebody is still at the keyboard, which is what keeps this account showing as available.
    /// Sent over the connection rather than as an HTTP request, which is the whole saving; a false says
    /// the tab is in the background and deliberately claims nothing - see LiveUpdatesHub.
    /// </summary>
    public async Task ReportPresenceAsync(bool isAtTheKeyboard)
    {
        if (_connection is not { State: HubConnectionState.Connected })
        {
            return;
        }

        try
        {
            await _connection.InvokeAsync(LiveUpdateMessages.ReportPresence, isAtTheKeyboard);
        }
        catch (Exception exception)
        {
            // The connection dropped mid-send. Presence ages on silence by design, and the reconnect
            // reports again - see UserPresence.
            _logger.LogDebug(exception, "Could not report presence over the live connection");
        }
    }

    private HubConnection BuildConnection()
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl, options => options.AccessTokenProvider = ProvideAccessTokenAsync)
            // Keeps trying rather than giving up after the default four attempts: a laptop that was
            // asleep for an hour should come back to a working app, not to one that quietly went back
            // to polling until the next reload.
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

    private void SubscribeToAnnouncements(HubConnection connection)
    {
        connection.On(LiveUpdateMessages.ChatChanged, () => ChatChanged?.Invoke());
        connection.On(LiveUpdateMessages.NotificationsChanged, () => NotificationsChanged?.Invoke());
        connection.On<Guid>(LiveUpdateMessages.PresenceChanged, userId => PresenceChanged?.Invoke(userId));
    }

    /// <summary>
    /// The token the handshake carries. Called again on every reconnect, which is what makes an
    /// expired one recoverable: a laptop that slept past the access token's lifetime refreshes here
    /// rather than reconnecting forever with a credential the server keeps refusing.
    /// </summary>
    private async Task<string?> ProvideAccessTokenAsync()
    {
        var token = await _tokenStore.GetTokenAsync();
        if (!string.IsNullOrEmpty(token))
        {
            return token;
        }

        return await _tokenRefreshService.TryRefreshAsync(CancellationToken.None)
            ? await _tokenStore.GetTokenAsync()
            : null;
    }

    public async ValueTask DisposeAsync() => await StopAsync();

    /// <summary>
    /// Backs off to half a minute and stays there, for as long as the app is open. The built-in policy
    /// stops after about thirty seconds, which is the wrong answer for a tab somebody leaves open all
    /// day: the thing it is waiting for - a network coming back, a server finishing a deploy - routinely
    /// takes longer than that, and giving up means the app is slower for the rest of the session with
    /// nothing on screen to say why.
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

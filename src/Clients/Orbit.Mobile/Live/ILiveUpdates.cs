namespace Orbit.Mobile.Live;

/// <summary>
/// What the app hears when something it is showing has changed, so it can stop asking every few
/// seconds.
///
/// An interface rather than the connection itself, because every screen that listens is a view model
/// this project can unit-test - see Screens/IScreenNavigator for the same reason. A test raises an
/// announcement; nothing has to stand a WebSocket up.
///
/// <b>Nothing here carries data.</b> Every announcement means "read again", and a screen answers it with
/// the same call its timer already made. That is what keeps end-to-end encrypted messages on exactly the
/// path they were on, and what makes a missed announcement cost a delay rather than a message - which is
/// why the screens keep a slow poll running underneath. See Orbit.Core's ILiveUpdatePublisher.
/// </summary>
public interface ILiveUpdates
{
    /// <summary>Something in this account's chat changed - a message, a receipt, or an approval.</summary>
    event Action? ChatChanged;

    /// <summary>Something arrived in, or left, this account's notification feed.</summary>
    event Action? NotificationsChanged;

    /// <summary>
    /// The connection came up or went down, so a screen can put its own timer back to the pace it used
    /// before there was one.
    /// </summary>
    event Action? ConnectionStateChanged;

    /// <summary>
    /// Whether announcements are actually arriving. Screens read this to decide how often to fall back
    /// to asking, and it is deliberately not assumed: a phone on a network that blocks WebSockets, or
    /// pointed at a server without the hub, has to work exactly as it did before rather than quietly
    /// stop updating.
    /// </summary>
    bool IsConnected { get; }

    /// <summary>
    /// Says somebody is still holding the phone, which is what keeps this account showing as available.
    /// Over the connection rather than as a request of its own, which is the saving - see PresenceReporter.
    /// Does nothing when there is no connection, and the caller falls back to the request it always made.
    /// </summary>
    Task<bool> TryReportPresenceAsync(bool isAtTheKeyboard);
}

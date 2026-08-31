using Orbit.Mobile.Live;

namespace Orbit.Mobile.Tests.TestDoubles;

/// <summary>
/// Announcements without a hub. A test says something changed; the screen listening answers it exactly
/// as it would answer the real connection - see ILiveUpdates for why the screens depend on the interface.
///
/// Silent and disconnected by default, which is the state every test that is not about live updates
/// should be in: the app has to work by asking, and a screen that only works when something tells it is
/// broken on the networks that will not carry a connection.
/// </summary>
internal sealed class AnnouncedLiveUpdates : ILiveUpdates
{
    public event Action? ChatChanged;

    public event Action? NotificationsChanged;

    public event Action? ConnectionStateChanged;

    public bool IsConnected { get; private set; }

    /// <summary>How many heartbeats went over the connection rather than as a request of their own.</summary>
    public int PresenceReports { get; private set; }

    public Task<bool> TryReportPresenceAsync(bool isAtTheKeyboard)
    {
        if (!IsConnected)
        {
            return Task.FromResult(false);
        }

        PresenceReports++;
        return Task.FromResult(true);
    }

    /// <summary>Brings the connection up or takes it down, as a train leaving a tunnel would.</summary>
    public void Becomes(bool connected)
    {
        IsConnected = connected;
        ConnectionStateChanged?.Invoke();
    }

    public void AnnounceChat() => ChatChanged?.Invoke();

    public void AnnounceNotifications() => NotificationsChanged?.Invoke();
}

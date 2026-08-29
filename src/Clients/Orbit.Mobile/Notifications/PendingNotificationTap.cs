namespace Orbit.Mobile.Notifications;

/// <summary>
/// Where a tapped notification wanted to go, and who gets to act on it.
///
/// There are two moments a tap can arrive, and they need opposite handling.
///
/// <b>Before the app has started.</b> Tapping a notification can *launch* Orbit, and the platform hands
/// the payload over long before there is a signed-in session or a screen to replace. Acting there would
/// land somewhere the startup flow then immediately replaces. So the destination is held, and the
/// startup flow takes it with <see cref="TakeAtStartup"/> when it decides where to open.
///
/// <b>While the app is already running.</b> The startup flow does not run again, so nothing would ever
/// take it - which is why <see cref="RecordedWhileRunning"/> exists. Found on a device: hooking this to
/// the window's Resumed event instead looks right and does not work, because iOS resumes the app
/// *before* it delivers the tap, so the holder is still empty when Resumed fires.
///
/// Deliberately a holder rather than only an event. An event raised before anybody subscribed is simply
/// lost, which is exactly the cold-start case - the one that has to work.
/// </summary>
public sealed class PendingNotificationTap
{
    private readonly Lock _gate = new();
    private string? _url;
    private bool _appHasStarted;

    /// <summary>
    /// A tap arriving while the app is already running, carrying its destination. Raised only then:
    /// before startup, the destination is held for <see cref="TakeAtStartup"/> instead, and raising it
    /// as well would have two things navigating at once.
    /// </summary>
    public event EventHandler<string>? RecordedWhileRunning;

    /// <summary>
    /// Called from platform code the moment a notification is tapped. A second tap before the first is
    /// followed replaces it: the reader's most recent choice is the one they meant.
    /// </summary>
    public void Record(string? url)
    {
        bool isRunning;
        lock (_gate)
        {
            _url = url;
            isRunning = _appHasStarted;
        }

        if (isRunning && url is { Length: > 0 })
        {
            RecordedWhileRunning?.Invoke(this, url);
        }
    }

    /// <summary>
    /// The destination the launch should follow, if there is one - and clears it, so it is followed once
    /// rather than every time somebody looks. Also marks the app as started: from here on a tap has
    /// nobody else to wait for, and is raised on <see cref="RecordedWhileRunning"/> instead.
    /// </summary>
    public string? TakeAtStartup()
    {
        lock (_gate)
        {
            _appHasStarted = true;
            var url = _url;
            _url = null;
            return url;
        }
    }
}

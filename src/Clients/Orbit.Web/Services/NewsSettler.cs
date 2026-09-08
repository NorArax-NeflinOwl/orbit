namespace Orbit.Web.Services;

/// <summary>
/// Marks the bell's unread entries read once the reader has reached what they are about - the server
/// first, then the shared set, so the badge clears as the page opens rather than on the next poll.
///
/// Its own class because two callers need it and they know different things. MainLayout settles what
/// the address bar says, on every navigation. A page settles what it *is* beyond its own address: an
/// appointment a task list raised is opened as that list's entry, while the reminder for it points at
/// the event - so reaching the entry is reaching the appointment, and nothing in the path says so.
/// That asymmetry is why pressing an event on the calendar used to leave its own notification lit.
/// </summary>
public sealed class NewsSettler
{
    private readonly NotificationsApiClient _notificationsApiClient;
    private readonly NotificationFeedState _notificationFeedState;

    public NewsSettler(NotificationsApiClient notificationsApiClient, NotificationFeedState notificationFeedState)
    {
        _notificationsApiClient = notificationsApiClient;
        _notificationFeedState = notificationFeedState;
    }

    /// <summary>
    /// Settles everything the given paths reach - see NotificationFeedState.UnreadUrlsSettledBy for what
    /// "reach" means. Returns whether anything was actually marked, so a caller can re-render only when
    /// something changed.
    ///
    /// Costs nothing when the bell is empty, which is the usual case: an ordinary click around the app
    /// should not spend a request per navigation.
    /// </summary>
    public async Task<bool> SettleAsync(params string[] paths)
    {
        if (_notificationFeedState.UnreadCount == 0)
        {
            return false;
        }

        var settled = paths
            .SelectMany(_notificationFeedState.UnreadUrlsSettledBy)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var anyMarked = false;
        foreach (var url in settled)
        {
            if (await _notificationsApiClient.MarkReadAtUrlAsync(url))
            {
                _notificationFeedState.MarkReadFor(url);
                anyMarked = true;
            }
        }

        return anyMarked;
    }
}

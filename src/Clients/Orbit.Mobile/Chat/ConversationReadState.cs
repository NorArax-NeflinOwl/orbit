namespace Orbit.Mobile.Chat;

/// <summary>
/// What one open conversation screen has told the server its reader has seen, and what to tell it next.
///
/// Having the screen open used to be the whole definition of "read": every sync marked everything, so a
/// conversation left open while the phone was in a pocket, or a poll that ran while the app sat in the
/// background, reported every message as read. Now a message counts as read once it has been on screen
/// - the page reports the last line the thread shows - while the screen is showing and the app is in the
/// foreground, and only up to the newest such message.
///
/// Kept apart from the view models and from MAUI so it can be tested without either, and so the
/// one-to-one and the group conversation decide it the same way. Orbit.Web's ChatReadState is the same
/// rule for the browser.
/// </summary>
public sealed class ConversationReadState
{
    private int? _lastVisibleIndex;

    /// <summary>The newest "read up to" the server has accepted from this screen, so it is not told twice.</summary>
    private DateTimeOffset? _toldUpToUtc;

    /// <summary>Whether the conversation page is the one on screen - set from its OnAppearing and OnDisappearing.</summary>
    public bool IsShowing { get; set; }

    /// <summary>
    /// Whether the app is in front of somebody at all. True until said otherwise: a page only appears in
    /// an app that is running in the foreground, and the window says when that stops.
    /// </summary>
    public bool IsAppInForeground { get; set; } = true;

    public bool IsInFront => IsShowing && IsAppInForeground;

    /// <summary>The last line the thread has shown, as an index into what the screen holds - what CollectionView.Scrolled reports.</summary>
    public void ShowedUpTo(int lastVisibleIndex) => _lastVisibleIndex = lastVisibleIndex;

    /// <summary>
    /// The "read up to" to send now, or null when there is nothing new to say - including whenever the
    /// screen is not in front of somebody, however much it is showing.
    ///
    /// It is the newest message somebody else wrote at or before the last line on screen. The reader's
    /// own messages, lines still waiting to send, and group announcements are not what the server marks,
    /// and an index past the end of what is held - a report from before the thread was redrawn shorter -
    /// counts as nothing seen rather than as everything. Never earlier than what was already told:
    /// scrolling back up does not make anything unread again.
    /// </summary>
    public DateTimeOffset? ReadUpToToTell(IReadOnlyList<ReadableChatMessage> lines)
    {
        if (!IsInFront || _lastVisibleIndex is not { } index || index < 0 || index >= lines.Count)
        {
            return null;
        }

        var lastSeenAtUtc = lines[index].SentAtUtc;
        DateTimeOffset? upToUtc = null;
        foreach (var line in lines)
        {
            if (line is { IsMine: false, IsAnnouncement: false, IsWaitingToSend: false, MessageId: not null }
                && line.SentAtUtc <= lastSeenAtUtc
                && (upToUtc is null || line.SentAtUtc > upToUtc))
            {
                upToUtc = line.SentAtUtc;
            }
        }

        return upToUtc is { } candidate && (_toldUpToUtc is null || candidate > _toldUpToUtc) ? candidate : null;
    }

    /// <summary>
    /// Records that the server took it. Only after it did: a mark that could not reach the server - no
    /// connection, which is ordinary on a phone - is offered again at the next chance rather than believed.
    /// </summary>
    public void Told(DateTimeOffset readUpToUtc)
    {
        if (_toldUpToUtc is null || readUpToUtc > _toldUpToUtc)
        {
            _toldUpToUtc = readUpToUtc;
        }
    }
}

using Orbit.Contracts.Chat;

namespace Orbit.Web.Services;

/// <summary>
/// What one open thread has told the server its reader has seen, and what it should tell it next.
///
/// "Read" used to mean "the thread was open": the page marked everything read on every poll, so a
/// conversation left open in a window nobody was sitting at reported every message as read. Now a
/// message counts as read once it has been on screen while the window was in front of somebody -
/// ChatSeenProbe answers both halves of that - and only up to the newest such message. Messages that
/// arrived below the bottom of the list stay unread until they are scrolled to.
///
/// The decision lives here rather than in the page or in chatSeen.js so it can be tested without a
/// browser, and so the one-to-one thread and the group thread make it the same way.
/// </summary>
public sealed class ChatReadState
{
    /// <summary>The newest "read up to" the server has accepted from this thread, so it is not told twice.</summary>
    private DateTimeOffset? _toldUpToUtc;

    /// <summary>
    /// The "read up to" to send now, or null when there is nothing new to say - including when nothing
    /// counts as seen at all.
    ///
    /// It is the newest message somebody else wrote at or before the one in view, not the one in view
    /// itself: the reader's own message at the bottom of the list says nothing the server needs, and
    /// sending its time would be a request that marks nothing. It is never earlier than what was
    /// already told - scrolling back up through a conversation does not make anything unread again.
    /// </summary>
    /// <param name="newestSeenMessageId">The newest message on screen while the window is in front, or null.</param>
    /// <param name="messages">What the thread holds - the seen message is looked up here for its SentAtUtc, which is what the server orders by.</param>
    public DateTimeOffset? ReadUpToToTell(
        Guid? newestSeenMessageId, IReadOnlyList<ChatMessageDto> messages, Guid ownUserId)
    {
        if (newestSeenMessageId is not { } seenId
            || messages.FirstOrDefault(message => message.Id == seenId) is not { } seen)
        {
            return null;
        }

        DateTimeOffset? upToUtc = null;
        foreach (var message in messages)
        {
            if (message.SenderUserId != ownUserId && message.SentAtUtc <= seen.SentAtUtc
                && (upToUtc is null || message.SentAtUtc > upToUtc))
            {
                upToUtc = message.SentAtUtc;
            }
        }

        return upToUtc is { } candidate && (_toldUpToUtc is null || candidate > _toldUpToUtc) ? candidate : null;
    }

    /// <summary>
    /// Whether the newest message somebody else wrote has been seen: it is the message in view, or came
    /// before it. What clears the bell's entries about the conversation. Each entry only says "a message
    /// arrived" - nothing says which message it was for - so they go once nothing the other party wrote
    /// is still below what has been seen, and not on the strength of the window being open.
    ///
    /// Nothing is remembered here, unlike <see cref="ReadUpToToTell"/>: the page asks the server only when
    /// the feed has something unread for the conversation (NotificationFeedState.HasUnreadFor), so asking
    /// this on every poll costs nothing.
    /// </summary>
    /// <param name="newestSeenMessageId">The newest message on screen while the window is in front, or null.</param>
    /// <param name="messages">What the thread holds.</param>
    public static bool HasSeenTheirNewest(
        Guid? newestSeenMessageId, IReadOnlyList<ChatMessageDto> messages, Guid ownUserId)
    {
        if (newestSeenMessageId is not { } seenId
            || messages.FirstOrDefault(message => message.Id == seenId) is not { } seen)
        {
            return false;
        }

        return !messages.Any(message => message.SenderUserId != ownUserId && message.SentAtUtc > seen.SentAtUtc);
    }

    /// <summary>
    /// Records that the server took it. Only after it did: a mark that failed on the way is sent again
    /// on the next chance rather than believed.
    /// </summary>
    public void Told(DateTimeOffset readUpToUtc)
    {
        if (_toldUpToUtc is null || readUpToUtc > _toldUpToUtc)
        {
            _toldUpToUtc = readUpToUtc;
        }
    }
}

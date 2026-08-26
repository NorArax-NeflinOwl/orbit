namespace Orbit.Mobile.Chat;

/// <summary>
/// One message as a conversation screen shows it.
/// </summary>
/// <param name="Text">
/// Null when it could not be opened - most often sealed under a key pair that has since been replaced.
/// The screen shows a placeholder for that one message rather than failing the whole conversation, which
/// is what Orbit.Web does too.
/// </param>
/// <param name="IsWaitingToSend">
/// Typed on this device and not yet accepted by the server. Shown alongside the real history so a
/// message written with no connection doesn't look lost.
/// </param>
/// <param name="SenderName">
/// Who wrote it, for a group conversation where that changes from message to message. Null for a
/// one-to-one one, where the screen's title already says who the other party is.
/// </param>
/// <param name="MessageId">
/// The server's id for this message, or null while it is still queued. What editing and deleting name.
/// </param>
/// <param name="GroupMessageId">
/// Shared by every copy of one group posting, null for a one-to-one message. An edit has to name the
/// whole posting rather than the single copy this device happens to hold.
/// </param>
/// <param name="ForwardedFromDisplayName">
/// Who originally wrote it, when this message reached the reader by being passed on. Null for anything
/// written directly to them.
/// </param>
/// <param name="IsReadByThem">
/// True when the other party has seen this one. Only ever set on the reader's own messages in a
/// one-to-one conversation: the server tracks reading per conversation, not per message, and offers it
/// for groups not at all.
/// </param>
public sealed record ReadableChatMessage(
    bool IsMine, string? Text, DateTimeOffset SentAtUtc, bool IsEdited, bool IsWaitingToSend,
    string? SenderName = null, Guid? MessageId = null, Guid? GroupMessageId = null, bool IsReadByThem = false,
    string? ForwardedFromDisplayName = null)
{
    /// <summary>True when this device could not open it - the screen shows a placeholder in its place.</summary>
    public bool CannotBeOpened => Text is null;

    /// <summary>Whether to label the bubble with its author, which only a group conversation does.</summary>
    public bool HasSenderName => SenderName is not null;

    public bool WasForwarded => ForwardedFromDisplayName is not null;

    /// <summary>
    /// Whether this can be passed on. Needs something to pass: a message that could not be opened here
    /// has no text to re-encrypt for somebody else, and one still queued has not been sent even once.
    /// </summary>
    public bool CanBeForwarded => Text is { Length: > 0 } && !IsWaitingToSend;

    /// <summary>
    /// Whether to offer editing and deleting. Only the reader's own messages, and only once the server
    /// has one - there is nothing to rewrite while it is still waiting to go out. The server decides for
    /// certain (a group admin may also delete somebody else's); this is what the screen offers.
    /// </summary>
    public bool CanBeChanged => IsMine && !IsWaitingToSend && MessageId is not null;
}

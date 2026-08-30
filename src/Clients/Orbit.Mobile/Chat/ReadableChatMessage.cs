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
    string? ForwardedFromDisplayName = null, bool? IsReadByEveryone = null,
    SharedItemInvitation? Invitation = null, EditAccessRequest? EditAccessRequest = null)
{
    /// <summary>
    /// True when this device could not open it - the screen shows a placeholder in its place. An offer
    /// to share something is not that: it opened perfectly well, and what it says is the offer.
    /// </summary>
    public bool CannotBeOpened => Text is null && Invitation is null && EditAccessRequest is null;

    /// <summary>
    /// When it was sent, on the reader's own clock and in their own language. Carried rather than
    /// formatted on screen: <see cref="SentAtUtc"/> is UTC, and XAML formatting a DateTimeOffset writes
    /// it in its own offset and the phone's culture - so a message sent at 14:41 in Warsaw read
    /// "12:41 PM" on a Polish screen. See EncryptedChatMessageReader, which writes this.
    /// </summary>
    public string SentAt { get; init; } = string.Empty;

    /// <summary>Whether this message is an offer to share something - see SharedItemInvitation.</summary>
    public bool IsInvitation => Invitation is not null;

    /// <summary>
    /// Set once the server has said this offer was already taken up - here or on another device. Not
    /// part of the message: it is a fact about the share, which outlives the message and changes
    /// without it. See ConversationViewModel, which asks.
    /// </summary>
    public bool WasAccepted { get; init; }

    /// <summary>
    /// Set when accepting came back refused. Deliberately not the same as <see cref="WasAccepted"/>:
    /// a withdrawn offer and an accepted one both stop being offers, but only one of them is now
    /// yours, and saying "already accepted" about the other would be untrue.
    /// </summary>
    public bool IsNoLongerOnOffer { get; init; }

    /// <summary>An offer still worth a button. One already taken up is a line, not a choice.</summary>
    public bool CanBeAccepted => IsInvitation && !WasAccepted && !IsNoLongerOnOffer;

    /// <summary>What the offer is called, for the line the screen shows in place of the text.</summary>
    public string InvitationName => Invitation?.Name ?? string.Empty;

    /// <summary>Whether this message is somebody asking to be allowed to change something of yours.</summary>
    public bool IsEditAccessRequest => EditAccessRequest is not null;

    public string EditAccessRequestName => EditAccessRequest?.Name ?? string.Empty;

    /// <summary>Whether to label the bubble with its author, which only a group conversation does.</summary>
    public bool HasSenderName => SenderName is not null;

    public bool WasForwarded => ForwardedFromDisplayName is not null;

    /// <summary>
    /// Whether this can be passed on. Needs something to pass: a message that could not be opened here
    /// has no text to re-encrypt for somebody else, and one still queued has not been sent even once.
    /// </summary>
    /// <summary>Whether the message has any action at all - what decides if it gets a menu trigger.</summary>
    public bool HasActions => CanBeChanged || CanBeForwarded || CanBeRepliedTo;

    public bool CanBeForwarded => Text is { Length: > 0 } && !IsWaitingToSend;

    /// <summary>
    /// What this message is answering, when it is a reply - see ReplyMessagePayload. Null for anything
    /// written to the conversation in general.
    /// </summary>
    public Guid? QuotedMessageId { get; init; }

    /// <summary>
    /// A short copy of what the answered message said, carried by the reply rather than looked up: the
    /// original may have been edited or deleted since, and the quote is still what was answered.
    /// </summary>
    public string? QuotedPreview { get; init; }

    public bool IsReply => QuotedPreview is not null;

    /// <summary>
    /// Whether this can be answered. Needs the same two things forwarding does, plus an id: the reply
    /// names what it answers, and a message the server has not seen has no name to give.
    /// </summary>
    public bool CanBeRepliedTo => CanBeForwarded && MessageId is not null;

    /// <summary>
    /// Whether to offer editing and deleting. Only the reader's own messages, and only once the server
    /// has one - there is nothing to rewrite while it is still waiting to go out. The server decides for
    /// certain (a group admin may also delete somebody else's); this is what the screen offers.
    /// </summary>
    public bool CanBeChanged => IsMine && !IsWaitingToSend && MessageId is not null;

    /// <summary>Whether this message belongs to a group conversation, which is what gives it a menu.</summary>
    public bool IsInAGroup => GroupMessageId is not null;

    /// <summary>
    /// Whether to say how far a group message got. Only on the reader's own, and only once it has been
    /// sent: there is nothing to report about a message still in the queue.
    /// </summary>
    public bool HasGroupDelivery => IsReadByEveryone is not null && !IsWaitingToSend;

    /// <summary>
    /// Read means every other member has seen it - one still behind and it is only delivered. Which is
    /// the honest reading: a group message that most of the group has read has not been read.
    /// </summary>
    public bool IsReadByTheWholeGroup => IsReadByEveryone is true;
}

namespace Orbit.Core.Chat;

/// <summary>
/// What became of one group message for one member. A group message is stored as a copy per recipient
/// (see ChatMessage.GroupMessageId), so this is that copy: it existing at all is what "delivered" means
/// here - the message reached the server addressed to them - and <see cref="ReadAtUtc"/> is when their
/// client said they had seen it.
///
/// A member who joined after the message was sent has no copy and so appears in no receipt: nothing was
/// ever addressed to them, which is the same reason they cannot read it.
/// </summary>
public sealed record GroupMessageReceipt(Guid RecipientUserId, DateTimeOffset? ReadAtUtc)
{
    public bool IsRead => ReadAtUtc is not null;
}

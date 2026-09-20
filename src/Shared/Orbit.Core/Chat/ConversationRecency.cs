namespace Orbit.Core.Chat;

/// <summary>
/// When there was last anything in a conversation. One rule, here rather than in either client, because
/// both draw a "Recent chats" card from it and they had come to answer the same question differently.
/// </summary>
public static class ConversationRecency
{
    /// <summary>
    /// The last message, or the last time that person was here, whichever is later.
    ///
    /// Asked for on 2026-09-18. The card said it was ordered by the most recently active conversation
    /// and measured that by the last message alone, so somebody who had been online an hour ago sat
    /// under a conversation nobody had touched for a week, saying "3 days ago" beside a name that had
    /// been about all morning. The two are different questions - one is about the conversation, the
    /// other about the person - and what the row is for is "when was there last anything here", which is
    /// the later of them.
    ///
    /// An account nobody has ever seen has no last-seen at all, and then the message is the whole
    /// answer. Takes the two moments rather than a contact, because Orbit.Core has no project references
    /// at all: the browser reads them off ContactDto and the phone off LocalContact, and this is the
    /// only place the comparison is made.
    /// </summary>
    public static DateTimeOffset LastAnythingIn(DateTimeOffset lastMessageAtUtc, DateTimeOffset? lastSeenAtUtc)
        => lastSeenAtUtc is { } lastSeen && lastSeen > lastMessageAtUtc ? lastSeen : lastMessageAtUtc;
}

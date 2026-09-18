using Orbit.Contracts.Chat;
using Orbit.Core.Users;

namespace Orbit.Web.Components;

/// <summary>
/// One row in the conversation list - a person or a group. Both are conversations to whoever is reading,
/// so both are one kind of thing here rather than two lists in two places.
/// </summary>
/// <param name="Id">The contact's user id, or the group's id.</param>
/// <param name="Note">What to say under the name: how many are waiting, or how many are in the group.</param>
public sealed record Conversation(
    Guid Id, string Name, bool IsGroup, int UnreadCount, PresenceStatus? Presence, string? Note)
{
    /// <summary>
    /// What to show next to a person right now. The API resolves it as the list is read, so an unreadable
    /// value can only mean a client and server that disagree about the names - offline is the honest
    /// answer to that, rather than claiming somebody is there.
    /// </summary>
    public static PresenceStatus PresenceOf(ContactDto contact)
        => Enum.TryParse<PresenceStatus>(contact.PresenceStatus, out var status) ? status : PresenceStatus.Offline;

    /// <summary>
    /// When there was last anything: the last message, or the last time this person was here, whichever
    /// is later (ContactDto.LastMessageAtUtc and LastSeenAtUtc).
    ///
    /// Asked for on 2026-09-18. "Recent chats" said it was ordered by the most recently active
    /// conversation and measured that by the last message alone, so somebody who had been online an hour
    /// ago sat under a conversation nobody had touched for a week, saying "3 days ago" beside a name that
    /// had been about all morning. The two answers are different questions - one is about the
    /// conversation, the other about the person - and what the row is for is "when was there last
    /// anything here", which is the later of them.
    ///
    /// An account nobody has ever seen has no last-seen at all, and then the message is the whole answer.
    /// </summary>
    public static DateTimeOffset LastAnythingFrom(ContactDto contact)
        => contact.LastSeenAtUtc is { } lastSeen && lastSeen > contact.LastMessageAtUtc
            ? lastSeen
            : contact.LastMessageAtUtc;
}

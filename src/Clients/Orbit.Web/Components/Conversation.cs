using Orbit.Contracts.Chat;
using Orbit.Core.Chat;
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
    /// When there was last anything here, off this browser's own row - see
    /// Orbit.Core.Chat.ConversationRecency.LastAnythingIn, which is where the rule itself lives so that
    /// the phone's card cannot come to answer it differently.
    /// </summary>
    public static DateTimeOffset LastAnythingFrom(ContactDto contact)
        => ConversationRecency.LastAnythingIn(contact.LastMessageAtUtc, contact.LastSeenAtUtc);
}

namespace Orbit.Core.Chat.Groups.GetChatGroups;

/// <summary>
/// One group as the caller's list of groups shows it: the group, and how many of its messages the caller
/// has not read yet - everything that arrived since they last had it open.
/// </summary>
public sealed record ChatGroupListing(ChatGroup Group, int UnreadCount);

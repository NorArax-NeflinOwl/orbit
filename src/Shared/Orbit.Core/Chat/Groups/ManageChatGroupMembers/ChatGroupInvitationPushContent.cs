using Orbit.Core.Notifications;

namespace Orbit.Core.Chat.Groups.ManageChatGroupMembers;

/// <summary>
/// What somebody sees when they are put into a group. Mirrors ChatMessagePushContent: the entry in the
/// feed is how a member finds out at all, since nothing else announces a group they did not open
/// themselves - and its url opens the group rather than the chat list.
/// </summary>
public static class ChatGroupInvitationPushContent
{
    public static PushNotificationPayload Build(Guid groupId, string groupName, string addedByDisplayName)
        => new("Added to a group", $"{addedByDisplayName} added you to {groupName}", $"/chat/groups/{groupId}");
}

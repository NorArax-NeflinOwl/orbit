namespace Orbit.Core.Notifications;

/// <summary>What triggered a NotificationEntry - lets the client render each kind slightly differently (e.g. a chat entry links straight to the conversation).</summary>
public enum NotificationEntryKind
{
    PushReminder,
    ChatMessage,

    /// <summary>
    /// Somebody shared a note, task list, calendar event, inventory or their position with this user.
    /// Recorded whenever it happens - the entry in the feed *is* the invitation, which is why it does
    /// not depend on NotificationSettings.AllowShareNotifications; that switch only adds push and email
    /// on top of it.
    /// </summary>
    SharedWithYou
}

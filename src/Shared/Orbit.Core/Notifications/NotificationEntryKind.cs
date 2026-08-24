namespace Orbit.Core.Notifications;

/// <summary>What triggered a NotificationEntry - lets the client render each kind slightly differently (e.g. a chat entry links straight to the conversation).</summary>
public enum NotificationEntryKind
{
    PushReminder,
    ChatMessage
}

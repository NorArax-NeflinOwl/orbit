namespace Orbit.Core.Notifications;

/// <summary>
/// Which delivery channel(s) a notification should go out on. A user picks this per notification
/// trigger (e.g. a calendar event's "on creation" and "before start" reminders, or a task item's
/// "overdue" and "daily reminder" notifications) instead of a single on/off switch, so they can choose
/// e-mail only, push only, both, or silence that trigger entirely without losing whatever lead
/// time/schedule is configured for it.
/// </summary>
[Flags]
public enum NotificationChannel
{
    None = 0,
    Email = 1,
    Push = 2,
    Both = Email | Push
}

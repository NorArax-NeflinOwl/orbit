using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks;

/// <summary>
/// When an entry says something, and where it says it: once it is late, and once a day until somebody
/// finishes it. Four settings that are always chosen together, read together by the two schedulers that
/// act on them, and stored together - so they travel as one thing rather than as four parameters on
/// every way of making a <see cref="TaskItem"/>.
/// </summary>
/// <param name="WhenOverdue">
/// Which channel(s), if any, tell the owner once the entry's due date has passed - see
/// <see cref="Orbit.Core.Tasks.OverdueNotifications.OverdueTaskNotificationScheduler"/>.
/// </param>
/// <param name="Daily">
/// Whether the entry comes back every day until it is turned off. It comes back as something still to
/// do each time, so finishing it today does not end it - see
/// <see cref="Orbit.Core.Tasks.DailyReminders.DailyTaskReminderScheduler"/>.
/// </param>
/// <param name="DailyChannel">Which channel(s) that daily reminder goes out on.</param>
/// <param name="DailyTimeOfDay">Local time of day it is sent at. Midnight when nobody has chosen one.</param>
public sealed record TaskItemReminders(
    NotificationChannel WhenOverdue,
    bool Daily,
    NotificationChannel DailyChannel,
    TimeOnly DailyTimeOfDay)
{
    /// <summary>
    /// What an entry nobody has said anything about does: speaks up when it is late, and says nothing
    /// daily. The same answers the four parameters used to default to on their own.
    /// </summary>
    public static readonly TaskItemReminders Default =
        new(NotificationChannel.Push, Daily: false, NotificationChannel.Push, default);
}

using Orbit.Core.Notifications;

namespace Orbit.Core.Tasks;

/// <summary>
/// A single checklist entry within a <see cref="TaskList"/>, with its own due date and completion
/// state - or, if <see cref="LinkedTaskListId"/> is set, a reference to another of the user's task
/// lists instead of an independently completable entry (see <see cref="LinkedTaskCompletionResolver"/>
/// for how its completion is derived, and <see cref="TaskListLinkValidator"/> for how the link itself
/// is validated).
/// </summary>
public sealed class TaskItem
{
    public Guid Id { get; private set; }
    public string Description { get; private set; }
    public DateTimeOffset? DueDateUtc { get; private set; }
    public bool IsCompleted { get; private set; }
    public Guid? LinkedTaskListId { get; private set; }

    /// <summary>
    /// Which channel(s), if any, notify the owner once this item becomes overdue - see
    /// <see cref="Orbit.Core.Tasks.OverdueNotifications.OverdueTaskNotificationScheduler"/>.
    /// </summary>
    public NotificationChannel OverdueNotificationChannel { get; private set; }

    /// <summary>
    /// When set, an incomplete item with a due date is reminded about once a day (at
    /// <see cref="DailyReminderTimeOfDay"/>, on <see cref="DailyReminderNotificationChannel"/>) until it's
    /// completed or the user turns this back off - see
    /// <see cref="Orbit.Core.Tasks.DailyReminders.DailyTaskReminderScheduler"/>.
    /// </summary>
    public bool RemindDaily { get; private set; }

    /// <summary>Which channel(s) the daily reminder above goes out on.</summary>
    public NotificationChannel DailyReminderNotificationChannel { get; private set; }

    /// <summary>Local time of day the daily reminder above is sent at. Defaults to midnight.</summary>
    public TimeOnly DailyReminderTimeOfDay { get; private set; }

    private TaskItem(
        Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted, Guid? linkedTaskListId,
        NotificationChannel overdueNotificationChannel, bool remindDaily, NotificationChannel dailyReminderNotificationChannel,
        TimeOnly dailyReminderTimeOfDay)
    {
        Id = id;
        Description = description;
        DueDateUtc = dueDateUtc;
        IsCompleted = isCompleted;
        LinkedTaskListId = linkedTaskListId;
        OverdueNotificationChannel = overdueNotificationChannel;
        RemindDaily = remindDaily;
        DailyReminderNotificationChannel = dailyReminderNotificationChannel;
        DailyReminderTimeOfDay = dailyReminderTimeOfDay;
    }

    /// <summary>
    /// A linked item's completion can't be set directly - it always follows the list it links to (see
    /// <see cref="LinkedTaskCompletionResolver"/>) - so <paramref name="isCompleted"/> is ignored in
    /// favor of "not completed" whenever <paramref name="linkedTaskListId"/> is set.
    /// </summary>
    public static TaskItem Create(
        string description, DateTimeOffset? dueDateUtc, bool isCompleted, Guid? linkedTaskListId = null,
        NotificationChannel overdueNotificationChannel = NotificationChannel.Push, bool remindDaily = false,
        NotificationChannel dailyReminderNotificationChannel = NotificationChannel.Push, TimeOnly dailyReminderTimeOfDay = default)
        => new(
            Guid.NewGuid(), description, dueDateUtc, linkedTaskListId is null && isCompleted, linkedTaskListId,
            overdueNotificationChannel, remindDaily, dailyReminderNotificationChannel, dailyReminderTimeOfDay);

    /// <summary>
    /// Rebuilds a checklist entry from already-known values, bypassing the completion override above -
    /// used both to reload an entry as persisted, and by <see cref="LinkedTaskCompletionResolver"/> to
    /// apply a freshly resolved completion value to a linked entry.
    /// </summary>
    public static TaskItem FromPersistence(
        Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted, Guid? linkedTaskListId,
        NotificationChannel overdueNotificationChannel, bool remindDaily, NotificationChannel dailyReminderNotificationChannel,
        TimeOnly dailyReminderTimeOfDay)
        => new(
            id, description, dueDateUtc, isCompleted, linkedTaskListId,
            overdueNotificationChannel, remindDaily, dailyReminderNotificationChannel, dailyReminderTimeOfDay);
}

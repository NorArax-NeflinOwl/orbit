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

    /// <summary>What this entry is, and so what else it carries - see <see cref="TaskItemKind"/>.</summary>
    public TaskItemKind Kind { get; private set; }

    /// <summary>
    /// Where a calendar entry happens, as the reader wrote it. Empty for every other kind, and empty
    /// for one tied to a calendar event: that event already holds the place, and a second copy here
    /// would be a second answer to the same question - see <see cref="LinkedCalendarEventId"/>.
    /// </summary>
    public string Location { get; private set; } = string.Empty;

    /// <summary>
    /// The calendar event this entry is the same appointment as, when it is one. The event is where the
    /// place and the time then live, so the two cannot drift apart.
    ///
    /// Not validated: an event that is later deleted leaves this pointing at nothing, and a client
    /// reading it treats that as "no event" - the same way a link to a deleted task list is treated as
    /// "not completed" rather than as a failure (see LinkedTaskCompletionResolver).
    /// </summary>
    public Guid? LinkedCalendarEventId { get; private set; }

    /// <summary>
    /// The shelf item this entry is an errand about, when it is one - see
    /// <see cref="TaskItemKind.Inventory"/>. Null for every other kind.
    ///
    /// Not validated, for the same reason <see cref="LinkedCalendarEventId"/> is not: an item deleted
    /// afterwards leaves this pointing at nothing, and a reader treats that as "no shelf item" rather
    /// than as a failure.
    /// </summary>
    public Guid? LinkedInventoryItemId { get; private set; }

    /// <summary>
    /// Which channel(s), if any, notify the owner once this item becomes overdue - see
    /// <see cref="Orbit.Core.Tasks.OverdueNotifications.OverdueTaskNotificationScheduler"/>.
    /// </summary>
    public NotificationChannel OverdueNotificationChannel { get; private set; }

    /// <summary>
    /// When set, this item is reminded about once a day (at <see cref="DailyReminderTimeOfDay"/>, on
    /// <see cref="DailyReminderNotificationChannel"/>) until the user turns it back off - and comes back
    /// as something still to do each time, so finishing it today does not end it. See
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
        TimeOnly dailyReminderTimeOfDay, TaskItemKind kind, string location, Guid? linkedCalendarEventId,
        Guid? linkedInventoryItemId)
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
        Kind = kind;
        LinkedCalendarEventId = kind == TaskItemKind.Calendar ? linkedCalendarEventId : null;
        LinkedInventoryItemId = kind == TaskItemKind.Inventory ? linkedInventoryItemId : null;
        Location = WhereItHappens(kind, location, LinkedCalendarEventId);
    }

    /// <summary>
    /// The place an entry keeps for itself. Only a calendar entry has one at all, and one tied to an
    /// event keeps none: the event holds the place, and storing it twice is how the two come to disagree
    /// - which is the whole reason the link exists.
    /// </summary>
    private static string WhereItHappens(TaskItemKind kind, string location, Guid? linkedCalendarEventId)
        => kind == TaskItemKind.Calendar && linkedCalendarEventId is null ? location.Trim() : string.Empty;

    /// <summary>
    /// Brings a finished entry back as something still to do, keeping its identity - the same row the
    /// reader already knows, rather than a second one beside it. Used where a task is meant to recur:
    /// an inventory item that is low again, and a daily reminder coming round.
    ///
    /// A linked entry is left alone: its completion follows the list it links to, and forcing it here
    /// would be overwritten by the next resolve anyway.
    /// </summary>
    public void Reopen()
    {
        if (LinkedTaskListId is null)
        {
            IsCompleted = false;
        }
    }

    /// <summary>
    /// Crosses an entry off. Used where something other than the reader's own tick establishes that the
    /// work is done - a warehouse that turns out to hold what the entry asks for.
    ///
    /// A linked entry is left alone for the same reason <see cref="Reopen"/> leaves it: its completion
    /// follows the list it points at.
    /// </summary>
    public void Complete()
    {
        if (LinkedTaskListId is null)
        {
            IsCompleted = true;
        }
    }

    /// <summary>
    /// A linked item's completion can't be set directly - it always follows the list it links to (see
    /// <see cref="LinkedTaskCompletionResolver"/>) - so <paramref name="isCompleted"/> is ignored in
    /// favor of "not completed" whenever <paramref name="linkedTaskListId"/> is set.
    /// </summary>
    public static TaskItem Create(
        string description, DateTimeOffset? dueDateUtc, bool isCompleted, Guid? linkedTaskListId = null,
        NotificationChannel overdueNotificationChannel = NotificationChannel.Push, bool remindDaily = false,
        NotificationChannel dailyReminderNotificationChannel = NotificationChannel.Push, TimeOnly dailyReminderTimeOfDay = default,
        TaskItemKind kind = TaskItemKind.Checklist, string location = "", Guid? linkedCalendarEventId = null,
        Guid? linkedInventoryItemId = null)
        => new(
            Guid.NewGuid(), description, dueDateUtc, linkedTaskListId is null && isCompleted, linkedTaskListId,
            overdueNotificationChannel, remindDaily, dailyReminderNotificationChannel, dailyReminderTimeOfDay,
            kind, location, linkedCalendarEventId, linkedInventoryItemId);

    /// <summary>
    /// Rebuilds a checklist entry from already-known values, bypassing the completion override above -
    /// used both to reload an entry as persisted, and by <see cref="LinkedTaskCompletionResolver"/> to
    /// apply a freshly resolved completion value to a linked entry.
    /// </summary>
    public static TaskItem FromPersistence(
        Guid id, string description, DateTimeOffset? dueDateUtc, bool isCompleted, Guid? linkedTaskListId,
        NotificationChannel overdueNotificationChannel, bool remindDaily, NotificationChannel dailyReminderNotificationChannel,
        TimeOnly dailyReminderTimeOfDay, TaskItemKind kind = TaskItemKind.Checklist, string location = "",
        Guid? linkedCalendarEventId = null, Guid? linkedInventoryItemId = null)
        => new(
            id, description, dueDateUtc, isCompleted, linkedTaskListId,
            overdueNotificationChannel, remindDaily, dailyReminderNotificationChannel, dailyReminderTimeOfDay,
            kind, location, linkedCalendarEventId, linkedInventoryItemId);
}

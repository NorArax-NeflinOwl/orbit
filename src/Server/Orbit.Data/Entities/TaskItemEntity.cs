namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a single checklist entry within a <see cref="TaskEntity"/>.
/// </summary>
public sealed class TaskItemEntity
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }

    /// <summary>
    /// Where this entry sits in its list. Stored because nothing else records it: saving a list deletes
    /// its rows and inserts them again, so without a position the order came back as whatever order the
    /// database happened to hold them in - which changed every time anything was saved, including
    /// ticking a box.
    /// </summary>
    public int Position { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset? DueDateUtc { get; set; }
    public bool IsCompleted { get; set; }

    /// <summary>
    /// Id of another <see cref="TaskEntity"/> this entry references instead of being independently
    /// completable - see <see cref="Orbit.Core.Tasks.LinkedTaskCompletionResolver"/>.
    /// </summary>
    public Guid? LinkedTaskListId { get; set; }

    /// <summary>What this entry is, stored by name like every other enum here - see Orbit.Core.Tasks.TaskItemKind.</summary>
    public string Kind { get; set; } = nameof(Orbit.Core.Tasks.TaskItemKind.Checklist);

    /// <summary>Where a calendar entry happens; empty for every other kind, and empty for one tied to an event - see Orbit.Core.Tasks.TaskItem.Location.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>The calendar event this entry is the same appointment as, if any - see Orbit.Core.Tasks.TaskItem.LinkedCalendarEventId.</summary>
    public Guid? LinkedCalendarEventId { get; set; }

    /// <summary>The shelf item this entry is an errand about, if any - see Orbit.Core.Tasks.TaskItem.LinkedInventoryItemId.</summary>
    public Guid? LinkedInventoryItemId { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Notifications.NotificationChannel"/> - "None"/"Email"/"Push"/"Both".</summary>
    public string OverdueNotificationChannel { get; set; } = "Push";

    public bool RemindDaily { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Notifications.NotificationChannel"/> - "None"/"Email"/"Push"/"Both".</summary>
    public string DailyReminderNotificationChannel { get; set; } = "Push";

    /// <summary>Local time of day the daily reminder is sent at, stored as minutes since midnight.</summary>
    public int DailyReminderTimeOfDayMinutes { get; set; }
}

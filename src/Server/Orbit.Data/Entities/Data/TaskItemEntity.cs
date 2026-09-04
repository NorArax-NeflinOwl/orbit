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
    /// The lists this entry references instead of being independently completable - see
    /// <see cref="Orbit.Core.Tasks.LinkedTaskCompletionResolver"/>. Empty for an ordinary entry.
    /// </summary>
    public List<TaskItemTaskListLinkEntity> LinkedTaskLists { get; set; } = [];

    /// <summary>What this entry is filed under - see Orbit.Core.Tasks.TaskItem.Categories. Empty for one nobody has filed.</summary>
    public List<TaskItemCategoryEntity> Categories { get; set; } = [];

    /// <summary>What this entry is, stored by name like every other enum here - see Orbit.Core.Tasks.TaskItemKind.</summary>
    public string Kind { get; set; } = nameof(Orbit.Core.Tasks.TaskItemKind.Checklist);

    /// <summary>Where a calendar entry happens; empty for every other kind, and empty for one tied to an event - see Orbit.Core.Tasks.TaskItem.Location.</summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>The calendar event this entry is the same appointment as, if any - see Orbit.Core.Tasks.TaskItem.LinkedCalendarEventId.</summary>
    public Guid? LinkedCalendarEventId { get; set; }

    /// <summary>The shelf item this entry is an errand about, if any - see Orbit.Core.Tasks.TaskItem.LinkedInventoryItemId.</summary>
    public Guid? LinkedInventoryItemId { get; set; }

    /// <summary>
    /// What this entry asks for, in the detail a shelf item is kept in - see
    /// Orbit.Core.Tasks.TaskItemProduct. Columns on the entry rather than a table of their own, unlike
    /// the categories and the linked lists: an entry describes one product at most, so there is nothing
    /// to have several rows of.
    ///
    /// All of them are written together or not at all, and <see cref="ProductUnit"/> is the one that
    /// says which: an entry describing nothing has no unit, and one describing something always has one,
    /// because a unit is what its amounts are counted in. Null on every entry that is not an inventory
    /// one, and on an inventory one that already stands for a real shelf item.
    /// </summary>
    public string? ProductType { get; set; }

    /// <summary>
    /// What the product is filed under, as many words as apply - a table of its own like the entry's own
    /// categories, and for the same reason. See TaskItemProductCategoryEntity for why it is not that
    /// same table. Empty for an entry that describes nothing, and for a product nobody filed.
    /// </summary>
    public List<TaskItemProductCategoryEntity> ProductCategories { get; set; } = [];

    public decimal? ProductQuantity { get; set; }

    public decimal? ProductMinimumQuantity { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Inventories.InventoryUnit"/>, and the flag saying the rest of these mean anything.</summary>
    public string? ProductUnit { get; set; }

    public DateTimeOffset? ProductExpiryDate { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Notifications.NotificationChannel"/> - "None"/"Email"/"Push"/"Both".</summary>
    public string? ProductExpiryNotificationChannel { get; set; }

    /// <summary>Something to look at every round - see Orbit.Core.Inventories.InventoryItem.IsCheckedRegularly.</summary>
    public bool? ProductIsCheckedRegularly { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Notifications.NotificationChannel"/> - "None"/"Email"/"Push"/"Both".</summary>
    public string OverdueNotificationChannel { get; set; } = "Push";

    public bool RemindDaily { get; set; }

    /// <summary>Serialized <see cref="Orbit.Core.Notifications.NotificationChannel"/> - "None"/"Email"/"Push"/"Both".</summary>
    public string DailyReminderNotificationChannel { get; set; } = "Push";

    /// <summary>Local time of day the daily reminder is sent at, stored as minutes since midnight.</summary>
    public int DailyReminderTimeOfDayMinutes { get; set; }
}

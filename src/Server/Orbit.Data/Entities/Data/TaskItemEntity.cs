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

    /// <summary>
    /// The longer text about this entry - see Orbit.Core.Tasks.TaskItem.Notes for why it is not called
    /// Description. Empty for an entry nobody wrote one on, which is most of them.
    /// </summary>
    public string Notes { get; set; } = string.Empty;

    public DateTimeOffset? DueDateUtc { get; set; }
    public bool IsCompleted { get; set; }

    /// <summary>
    /// How much this entry matters, stored by name like every other enum here - see
    /// Orbit.Core.Abstractions.ItemPriority. The list has one of its own; this is the entry's, because a
    /// list of ten errands usually has one that has to happen and nine that can wait.
    /// </summary>
    public string Priority { get; set; } = nameof(Orbit.Core.Abstractions.ItemPriority.Normal);

    /// <summary>
    /// What colour this entry is drawn in, as a CSS colour the client wrote - the same shape a calendar
    /// event's own colour takes (see CalendarEventEntity.Color). Empty for an entry nobody chose one
    /// for, which is most of them, and which every screen reads as "the colour this kind is drawn in".
    /// </summary>
    public string Colour { get; set; } = string.Empty;

    /// <summary>
    /// Closed without being done - see Orbit.Core.Tasks.TaskItem.IsFailed. Its own column beside the
    /// tick rather than a status replacing it: every query that asks whether an entry is ticked still
    /// means the same thing by it, and an existing row reads as "not failed" without being rewritten.
    /// </summary>
    public bool IsFailed { get; set; }

    /// <summary>
    /// When this entry was ticked off - see Orbit.Core.Tasks.TaskItem.CompletedAtUtc. Null for one that is
    /// not done, and for every entry ticked before this column existed: the time was not kept then, and
    /// the migration leaves those empty rather than inventing one.
    /// </summary>
    public DateTimeOffset? CompletedAtUtc { get; set; }

    /// <summary>
    /// The lists this entry references instead of being independently completable - see
    /// <see cref="Orbit.Core.Tasks.LinkedTaskCompletionResolver"/>. Empty for an ordinary entry.
    /// </summary>
    public List<TaskItemTaskListLinkEntity> LinkedTaskLists { get; set; } = [];

    /// <summary>
    /// The entries of the same list this one waits for - see Orbit.Core.Tasks.TaskItem.WaitsForTaskItemIds.
    /// Empty for an ordinary entry, which is nearly all of them.
    /// </summary>
    public List<TaskItemStepEntity> Steps { get; set; } = [];

    /// <summary>
    /// The ways this entry can be got done, any one of which is enough - see
    /// Orbit.Core.Tasks.TaskItem.Alternatives. Empty for an ordinary entry, which is nearly all of them.
    /// </summary>
    public List<TaskItemAlternativeEntity> Alternatives { get; set; } = [];

    /// <summary>When this entry was first stored - see Orbit.Core.Tasks.TaskItem.CreatedAtUtc. Null for rows written before it was kept.</summary>
    public DateTimeOffset? CreatedAtUtc { get; set; }

    /// <summary>
    /// The entry this one is the same thing as - see Orbit.Core.Tasks.TaskItem.ReferencesTaskItemId. No
    /// foreign key: the source may be on another list, and when it goes, TaskItemReferences hands its
    /// role on before the row does.
    /// </summary>
    public Guid? ReferencesTaskItemId { get; set; }

    /// <summary>How much of its product this entry needs - see Orbit.Core.Tasks.TaskItem.RequiredQuantity.</summary>
    public decimal? RequiredQuantity { get; set; }

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

namespace Orbit.Contracts.Tasks;

/// <summary>
/// OverdueNotificationChannel and DailyReminderNotificationChannel are each one of "None"/"Email"/
/// "Push"/"Both" (matches Orbit.Core.Notifications.NotificationChannel). DailyReminderTimeOfDay is the
/// local time of day the daily reminder is sent at when RemindDaily is set.
///
/// Kind is "Checklist", "Calendar" or "Inventory" - see Orbit.Core.Tasks.TaskItemKind. Location says where a calendar
/// entry happens; it is ignored for every other kind, and for one carrying a LinkedCalendarEventId,
/// since that event already holds the place.
/// </summary>
public sealed record TaskItemDto(
    Guid Id,
    string Description,
    DateTimeOffset? DueDateUtc,
    bool IsCompleted,
    /// <summary>
    /// The first list this entry stands for, or null. The old shape, kept because a client that has not
    /// learned about the new one still reads and writes this - see <see cref="LinkedTaskListIds"/>.
    /// </summary>
    Guid? LinkedTaskListId,
    string OverdueNotificationChannel,
    bool RemindDaily,
    string DailyReminderNotificationChannel,
    TimeOnly DailyReminderTimeOfDay,
    string Kind = "Checklist",
    string Location = "",
    Guid? LinkedCalendarEventId = null,
    /// <summary>
    /// The shelf item an Inventory entry is an errand about - see Orbit.Core.Tasks.TaskItem.LinkedInventoryItemId.
    /// Null for every other kind.
    /// </summary>
    Guid? LinkedInventoryItemId = null,
    /// <summary>
    /// Every list this entry stands for, in order - see Orbit.Core.Tasks.TaskItem.LinkedTaskListIds.
    /// Always sent; LinkedTaskListId above repeats the first of them for older clients.
    /// </summary>
    IReadOnlyList<Guid>? LinkedTaskListIds = null,
    /// <summary>
    /// What this entry is about, in the reader's own words, and as many as apply - see
    /// Orbit.Core.Tasks.TaskItem.Categories. Null means "not provided", which is what a client written
    /// before categories existed sends; an empty list means "none", and clears them.
    /// </summary>
    IReadOnlyList<string>? Categories = null,
    /// <summary>
    /// What an Inventory entry asks for, until it stands for a real shelf item - see
    /// <see cref="TaskItemProductDto"/>. Null for every other entry, and for one that already has a
    /// shelf item behind it.
    /// </summary>
    TaskItemProductDto? Product = null)
{
    /// <summary>
    /// Whichever shape the sender used, read as one. Needed on the way in as well as the way out: a
    /// client written before an entry could name several lists sends only the single field.
    /// </summary>
    public IReadOnlyList<Guid> AllLinkedTaskListIds
        => LinkedTaskListIds is { Count: > 0 } ids ? ids : LinkedTaskListId is { } single ? [single] : [];

    /// <summary>The categories as something to read without a null check - see <see cref="Categories"/>.</summary>
    public IReadOnlyList<string> AllCategories => Categories ?? [];
}

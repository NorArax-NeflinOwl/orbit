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
    TaskItemProductDto? Product = null,
    /// <summary>
    /// The longer text about this entry, where the Description above is what it is called - see
    /// Orbit.Core.Tasks.TaskItem.Notes for why the two are named this way round. Empty for an entry
    /// nobody wrote one on; on the way out it is always sent.
    /// </summary>
    string? Notes = null,
    /// <summary>
    /// Closed without being done - see Orbit.Core.Tasks.TaskItem.IsFailed. Never true together with
    /// <paramref name="IsCompleted"/>, and never true for an entry standing for other lists. A client
    /// written before the cross existed reads such an entry as one still to do, which is the safest
    /// thing it can be wrong about.
    /// </summary>
    bool IsFailed = false,
    /// <summary>
    /// The entries <b>on the same list</b> this one waits for - see
    /// Orbit.Core.Tasks.TaskItem.WaitsForTaskItemIds. Empty for nearly every entry. An entry waiting on
    /// something unfinished cannot be ticked off, which the server enforces however it is asked.
    /// </summary>
    IReadOnlyList<Guid>? WaitsForTaskItemIds = null,
    /// <summary>
    /// How much this entry matters - one of "Low", "Normal", "High", see
    /// Orbit.Core.Abstractions.ItemPriority. The list it is on has one of its own and this is not it: a
    /// list of ten errands usually has one that has to happen and nine that can wait. <b>Null means "not
    /// provided"</b>, which leaves the stored answer alone - the rule every field added since the phone
    /// stopped knowing about all of them follows.
    /// </summary>
    string? Priority = null,
    /// <summary>
    /// What colour the entry is drawn in, as a CSS colour - the same shape a calendar event's own colour
    /// takes. Null means "not provided" like Priority above; an empty string means "no colour of its
    /// own", which every screen reads as "whatever this kind is drawn in".
    /// </summary>
    string? Colour = null,
    /// <summary>
    /// The ways this entry can be got done, any one of which is enough - see
    /// Orbit.Core.Tasks.TaskItem.Alternatives. Null or empty for an ordinary entry. While it holds any,
    /// <see cref="IsCompleted"/> is theirs rather than the entry's own: true exactly when one is done.
    /// </summary>
    IReadOnlyList<TaskItemAlternativeDto>? Alternatives = null,
    /// <summary>
    /// The entry this one is the same thing as - see Orbit.Core.Tasks.TaskItem.ReferencesTaskItemId.
    /// Always the group's source. Null for an entry of its own.
    /// </summary>
    Guid? ReferencesTaskItemId = null,
    /// <summary>How much of its product this entry needs - see Orbit.Core.Tasks.TaskItem.RequiredQuantity.</summary>
    decimal? RequiredQuantity = null)
{
    /// <summary>The ways as something to read without a null check - see <see cref="Alternatives"/>.</summary>
    public IReadOnlyList<TaskItemAlternativeDto> AllAlternatives => Alternatives ?? [];

    /// <summary>
    /// An entry as a save sends it, kept where no server reads it - inside a private list's sealed payload
    /// (see Orbit.Web's TasksApiClient.SealIfPrivateAsync), where every field has to travel or it is gone.
    ///
    /// The other half of <see cref="TaskItemRequest.From"/>, and here for the same reason: the sealing used
    /// to list the fields by hand, and every one added after it was written - the kind, the place, both
    /// links, the categories, the product, the description, the cross, the steps, the look - was silently
    /// dropped from every private list saved in a browser. One mapping means a field added later is carried.
    /// </summary>
    /// <param name="id">The entry's id - its own, or one minted for an entry that has never been saved.</param>
    public static TaskItemDto From(TaskItemRequest item, Guid id)
        => new(
            id,
            item.Description,
            item.DueDateUtc,
            item.IsCompleted,
            // Written into the new field only: the single one carries just the first list, and a private
            // list would quietly lose the rest.
            LinkedTaskListId: null,
            item.OverdueNotificationChannel,
            item.RemindDaily,
            item.DailyReminderNotificationChannel,
            item.DailyReminderTimeOfDay,
            item.Kind,
            item.Location,
            item.LinkedCalendarEventId,
            item.LinkedInventoryItemId,
            item.AllLinkedTaskListIds,
            item.Categories,
            item.Product,
            item.Notes,
            item.IsFailed,
            item.WaitsForTaskItemIds,
            item.Priority,
            item.Colour,
            item.Alternatives,
            item.ReferencesTaskItemId,
            item.RequiredQuantity);

    /// <summary>
    /// Whichever shape the sender used, read as one. Needed on the way in as well as the way out: a
    /// client written before an entry could name several lists sends only the single field.
    /// </summary>
    public IReadOnlyList<Guid> AllLinkedTaskListIds
        => LinkedTaskListIds is { Count: > 0 } ids ? ids : LinkedTaskListId is { } single ? [single] : [];

    /// <summary>The categories as something to read without a null check - see <see cref="Categories"/>.</summary>
    public IReadOnlyList<string> AllCategories => Categories ?? [];

    /// <summary>The description as something to read without a null check - see <see cref="Notes"/>.</summary>
    public string AllNotes => Notes ?? string.Empty;

    /// <summary>The steps as something to read without a null check - see <see cref="WaitsForTaskItemIds"/>.</summary>
    public IReadOnlyList<Guid> AllWaitsForTaskItemIds => WaitsForTaskItemIds ?? [];
}

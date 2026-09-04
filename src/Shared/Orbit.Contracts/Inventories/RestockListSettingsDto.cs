namespace Orbit.Contracts.Inventories;

/// <summary>
/// How an inventory's restock list is built and when it comes round - see
/// Orbit.Core.Inventories.RestockListSettings, which explains what each choice means.
/// </summary>
/// <param name="RefreshTimeOfDay">Local time of day, "HH:mm". Means nothing while RemindDaily is false.</param>
/// <param name="IsEnabled">
/// Whether the inventory keeps a restock list at all. Saving false **deletes** the managed list and
/// everything on it; saving true again builds a fresh one. Defaulted true so a client that has not been
/// taught about this field yet cannot switch it off by omission.
/// </param>
/// <param name="RemindDaily">Whether the list carries the standing daily "Update stock levels" reminder.</param>
/// <param name="ListPriority">ItemPriority by name - "Low", "Normal" or "High".</param>
/// <param name="OnlyCheckedRegularly">
/// Whether the list asks only about the products marked to look at every round, rather than about
/// everything the shelf says is running low. Defaulted false so a client that has not been taught about
/// this field cannot narrow the list by omission.
/// </param>
/// <param name="ReminderChannel">
/// Where the standing daily reminder is said - "None"/"Email"/"Push"/"Both", matching
/// Orbit.Core.Notifications.NotificationChannel. Means nothing while RemindDaily is false.
/// </param>
public sealed record RestockListSettingsDto(
    bool OnlyLinkedWithDueDate,
    TimeOnly RefreshTimeOfDay,
    bool IsEnabled = true,
    bool RemindDaily = true,
    string ListPriority = "Normal",
    bool OnlyCheckedRegularly = false,
    string ReminderChannel = "Push");

/// <summary>What rebuilding the list moved - see Orbit.Core.Inventories.RestockRefreshOutcome.</summary>
public sealed record RestockRefreshResultDto(int AddedCount, int RemovedCount);

namespace Orbit.Contracts.Inventories;

/// <summary>
/// How an inventory's restock list is built and when it comes round - see
/// Orbit.Core.Inventories.RestockListSettings, which explains what each choice means.
/// </summary>
/// <param name="RefreshTimeOfDay">Local time of day, "HH:mm".</param>
public sealed record RestockListSettingsDto(bool OnlyLinkedWithDueDate, TimeOnly RefreshTimeOfDay);

/// <summary>What rebuilding the list moved - see Orbit.Core.Inventories.RestockRefreshOutcome.</summary>
public sealed record RestockRefreshResultDto(int AddedCount, int RemovedCount);

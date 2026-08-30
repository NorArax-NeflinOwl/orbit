namespace Orbit.Contracts.Inventory;

/// <summary>
/// How a warehouse's restock list is built and when it comes round - see
/// Orbit.Core.Inventory.RestockListSettings, which explains what each choice means.
/// </summary>
/// <param name="RefreshTimeOfDay">Local time of day, "HH:mm".</param>
public sealed record RestockListSettingsDto(bool OnlyLinkedWithDueDate, TimeOnly RefreshTimeOfDay);

/// <summary>What rebuilding the list moved - see Orbit.Core.Inventory.RestockRefreshOutcome.</summary>
public sealed record RestockRefreshResultDto(int AddedCount, int RemovedCount);

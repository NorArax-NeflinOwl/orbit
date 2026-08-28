namespace Orbit.Contracts.Tasks;

/// <summary>Points a task list at the warehouse its work is measured against. A null WarehouseId unlinks it.</summary>
public sealed record LinkTaskListToWarehouseRequest(Guid? WarehouseId);

/// <param name="Required">How many the work calls for - one per line saying so, so repetition is quantity.</param>
/// <param name="Available">How many the warehouse holds under that name.</param>
/// <param name="Missing">The difference, or zero when the shelf covers it.</param>
public sealed record StockRequirementDto(string Name, decimal Required, decimal Available, decimal Missing);

/// <param name="IsAchievable">Nothing falls short - the work can be started with what is on the shelf.</param>
public sealed record TaskListStockCheckDto(bool IsAchievable, IReadOnlyList<StockRequirementDto> Requirements);

/// <param name="AddedCount">How many entries were put on the restock list - zero when nothing was short, or when everything short was already waiting there.</param>
public sealed record RaiseStockShortfallsResultDto(int AddedCount);

/// <summary>What bringing a list and its warehouse back into step actually moved.</summary>
/// <param name="CrossedOffCount">Entries the warehouse turned out to cover, and so finished.</param>
/// <param name="AddedCount">Products the warehouse held that no list mentioned, and so put on one.</param>
public sealed record StockReconciliationResultDto(int CrossedOffCount, int AddedCount);

/// <summary>How many shelf items were brought up to their minimum by finishing a restock list.</summary>
public sealed record FinishRestockingResultDto(int ToppedUpCount);

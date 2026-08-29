namespace Orbit.Contracts.Inventory;

/// <summary>
/// One row of the warehouse editor's item list on its way back to the server. Id is null for a row the
/// user just added; existing rows send theirs back so the item keeps its identity - and its open restock
/// task - across a save. Mirrors TaskItemDto's role in the task list editor.
/// </summary>
public sealed record WarehouseItemDto(
    Guid? Id,
    string Name,
    string ProductType,
    string Category,
    decimal Quantity,
    decimal? MinimumQuantity,
    /// <summary>Serialized Orbit.Core.Inventory.InventoryUnit - what the two amounts above are counted in.</summary>
    string Unit,
    DateTimeOffset? ExpiryDate,
    string ExpiryNotificationChannel);

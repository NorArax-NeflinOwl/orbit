namespace Orbit.Contracts.Inventory;

/// <summary>
/// Body for creating a warehouse (name only - Items is empty) and for saving one (name plus its whole
/// intended item list, since items missing from Items are deleted).
/// </summary>
public sealed record SaveWarehouseRequest(string Name, IReadOnlyList<WarehouseItemDto> Items);

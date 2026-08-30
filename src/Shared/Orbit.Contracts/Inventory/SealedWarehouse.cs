namespace Orbit.Contracts.Inventory;

/// <inheritdoc cref="Orbit.Contracts.Notes.SealedNote"/>
public sealed record SealedWarehouse(string Name, IReadOnlyList<WarehouseItemDto> Items);

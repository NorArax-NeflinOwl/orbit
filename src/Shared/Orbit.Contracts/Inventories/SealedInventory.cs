namespace Orbit.Contracts.Inventories;

/// <inheritdoc cref="Orbit.Contracts.Notes.SealedNote"/>
public sealed record SealedInventory(string Name, IReadOnlyList<InventoryItemRequest> Items);

namespace Orbit.Contracts.Inventories;

/// <summary>
/// One task entry asking for one shelf item - see Orbit.Core.Inventories.ShelfClaim. Read before a save
/// of the inventory, so a reader changing an amount is told whether it will reach a list, and which
/// lists it would have to be divided between if more than one is asking.
/// </summary>
/// <param name="Quantity">
/// How much this one entry asks for. The shelf item's Usage is these added up - see
/// Orbit.Contracts.Inventories.InventoryItemDto.Usage.
/// </param>
public sealed record ShelfClaimDto(
    Guid InventoryItemId, Guid TaskListId, string TaskListName, Guid TaskItemId, string Description, decimal Quantity);

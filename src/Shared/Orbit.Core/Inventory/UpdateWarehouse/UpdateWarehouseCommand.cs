using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.UpdateWarehouse;

/// <summary>
/// Saves a warehouse and its whole item list in one go, the way UpdateTaskListCommand saves a task list
/// and its items. Items missing from Items are deleted, so this is the full intended contents rather
/// than a patch.
///
/// For a private warehouse, Name and Items travel empty and the real values are sealed inside
/// EncryptedContent - see Warehouse.IsPrivate.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateWarehouseCommand(
    Guid UserId, Guid WarehouseId, string Name, IReadOnlyList<WarehouseItemInput> Items,
    bool IsPrivate, EncryptedPayload? EncryptedContent,
    /// <summary>Null leaves the stored description alone - see SaveWarehouseRequest.</summary>
    string? Description = null) : IRequest<EditOutcome>;

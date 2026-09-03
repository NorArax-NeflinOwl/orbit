using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.UpdateInventory;

/// <summary>
/// Saves an inventory and its whole item list in one go, the way UpdateTaskListCommand saves a task list
/// and its items. Items missing from Items are deleted, so this is the full intended contents rather
/// than a patch.
///
/// For a private inventory, Name and Items travel empty and the real values are sealed inside
/// EncryptedContent - see Inventory.IsPrivate.
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateInventoryCommand(
    Guid UserId, Guid InventoryId, string Name, IReadOnlyList<InventoryItemInput> Items,
    bool IsPrivate, EncryptedPayload? EncryptedContent,
    /// <summary>Null leaves the stored description alone - see SaveInventoryRequest.</summary>
    string? Description = null) : IRequest<EditOutcome>;

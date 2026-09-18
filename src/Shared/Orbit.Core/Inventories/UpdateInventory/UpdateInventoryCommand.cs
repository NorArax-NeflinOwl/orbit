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
    string? Description = null,
    /// <summary>
    /// The shelf items whose new amount is to be divided equally between every entry asking for them -
    /// the reader's answer to the warning a shared shelf row raises. See ShelfDemand: a row asked for by
    /// one entry is written back to it whatever this says, and a shared one named nowhere here is left
    /// alone, which is what "I'll change the list myself" means.
    /// </summary>
    IReadOnlyList<Guid>? SplitEvenlyAcross = null) : IRequest<EditOutcome>;

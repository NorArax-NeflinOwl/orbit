using Orbit.Contracts;

namespace Orbit.Contracts.Inventories;

/// <summary>
/// Body for creating an inventory (name only - Items is empty) and for saving one (name plus its whole
/// intended item list, since items missing from Items are deleted).
///
/// IsPrivate marks an inventory only its owner can read: Name and Items then travel empty and the real
/// values are sealed inside EncryptedContent, which the browser fills in and the server never opens.
/// </summary>
/// <param name="Description">
/// What the inventory is, under its name. <b>Null means "not provided", and leaves whatever is stored
/// alone</b>; an empty string means "cleared" - see UpdateTaskRequest for why the two differ.
/// </param>
/// <param name="FolderId">
/// Where to file it as it is made - the tab the reader is standing on. <b>Read only when the inventory
/// is being created</b>: a save is this same body, and filing an inventory that already exists is its
/// own endpoint (PUT /api/inventories/{id}/folder), so that null here can go on meaning "nothing said"
/// rather than "take it out of its folder" - see Orbit.Core.Inventories.Inventory.MoveToFolder.
/// </param>
public sealed record SaveInventoryRequest(
    string Name, IReadOnlyList<InventoryItemRequest> Items, bool IsPrivate = false,
    EncryptedContentDto? EncryptedContent = null, string? Description = null, Guid? FolderId = null);

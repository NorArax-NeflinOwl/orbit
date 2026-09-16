using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.CreateInventory;

/// <param name="Items">
/// What is already on the shelf. Usually empty - most inventories are named and then filled - but the
/// editor at /inventory/new is a name-it-and-fill-it screen, and refusing the rows it collected was a
/// save that could never succeed. Ignored for a private inventory, whose contents travel sealed inside
/// <paramref name="EncryptedContent"/> and must leave no readable row behind.
/// </param>
/// <param name="FolderId">
/// The folder to file it under as it is made, or null for none - which is how something new lands in the
/// tab the reader is standing on (see Orbit.Core.Folders.FolderScope). Defaulted and last, so a client
/// that has not heard of folders on the inventories still creates them.
/// </param>
[ClientAction(ClientActionCategory.Save)]
public sealed record CreateInventoryCommand(
    Guid UserId, string Name, bool IsPrivate = false, EncryptedPayload? EncryptedContent = null,
    string? Description = null, IReadOnlyList<InventoryItemInput>? Items = null,
    Guid? FolderId = null) : IRequest<Guid>;

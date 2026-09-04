using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.CreateInventory;

/// <param name="Items">
/// What is already on the shelf. Usually empty - most inventories are named and then filled - but the
/// editor at /inventory/new is a name-it-and-fill-it screen, and refusing the rows it collected was a
/// save that could never succeed. Ignored for a private inventory, whose contents travel sealed inside
/// <paramref name="EncryptedContent"/> and must leave no readable row behind.
/// </param>
[ClientAction(ClientActionCategory.Save)]
public sealed record CreateInventoryCommand(
    Guid UserId, string Name, bool IsPrivate = false, EncryptedPayload? EncryptedContent = null,
    string? Description = null, IReadOnlyList<InventoryItemInput>? Items = null) : IRequest<Guid>;

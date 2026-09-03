using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.CreateInventory;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateInventoryCommand(
    Guid UserId, string Name, bool IsPrivate = false, EncryptedPayload? EncryptedContent = null,
    string? Description = null) : IRequest<Guid>;

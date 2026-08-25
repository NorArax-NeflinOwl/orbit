using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.CreateWarehouse;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateWarehouseCommand(
    Guid UserId, string Name, bool IsPrivate = false, EncryptedPayload? EncryptedContent = null) : IRequest<Guid>;

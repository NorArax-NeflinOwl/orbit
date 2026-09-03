using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.DeleteInventory;

public sealed record DeleteInventoryCommand(Guid UserId, Guid InventoryId) : IRequest<bool>;

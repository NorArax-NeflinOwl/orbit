using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.UpdateWarehouse;

[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateWarehouseCommand(Guid UserId, Guid WarehouseId, string Name) : IRequest<EditOutcome>;

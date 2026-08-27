using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.LinkTaskListToWarehouse;

/// <summary>Points a task list at the warehouse its work is measured against, or at none when WarehouseId is null.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record LinkTaskListToWarehouseCommand(Guid UserId, Guid TaskListId, Guid? WarehouseId) : IRequest<bool>;

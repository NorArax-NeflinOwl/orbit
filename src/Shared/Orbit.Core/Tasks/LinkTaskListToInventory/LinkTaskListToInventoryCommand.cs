using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.LinkTaskListToInventory;

/// <summary>Points a task list at the inventory its work is measured against, or at none when InventoryId is null.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record LinkTaskListToInventoryCommand(Guid UserId, Guid TaskListId, Guid? InventoryId) : IRequest<bool>;

using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GenerateWarehouseFromTaskList;

/// <summary>
/// Makes a warehouse holding one entry per distinct thing a task list's work calls for, and points the
/// list at it. Returns the new warehouse's id, or null when there is no such list.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record GenerateWarehouseFromTaskListCommand(Guid UserId, Guid TaskListId) : IRequest<Guid?>;

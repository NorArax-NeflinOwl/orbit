using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GenerateInventoryFromTaskList;

/// <summary>
/// Makes an inventory holding one entry per distinct thing a task list's work calls for, and points the
/// list at it. Returns the new inventory's id, or null when there is no such list.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record GenerateInventoryFromTaskListCommand(Guid UserId, Guid TaskListId) : IRequest<Guid?>;

using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.UpdateTaskList;

[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateTaskListCommand(Guid UserId, Guid Id, string Title, IReadOnlyList<TaskItem> Items, bool IsGroup) : IRequest<EditOutcome>;

using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.UpdateTaskList;

public sealed record UpdateTaskListCommand(Guid UserId, Guid Id, string Title, IReadOnlyList<TaskItem> Items) : IRequest<bool>;

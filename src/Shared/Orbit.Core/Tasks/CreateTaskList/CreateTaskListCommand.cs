using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CreateTaskList;

public sealed record CreateTaskListCommand(Guid UserId, string Title, IReadOnlyList<TaskItem> Items) : IRequest<Guid>;

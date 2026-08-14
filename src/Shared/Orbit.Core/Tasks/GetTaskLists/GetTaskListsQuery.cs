using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GetTaskLists;

public sealed record GetTaskListsQuery(Guid UserId) : IRequest<IReadOnlyList<TaskList>>;

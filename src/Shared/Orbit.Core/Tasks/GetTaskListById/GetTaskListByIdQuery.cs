using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.GetTaskListById;

public sealed record GetTaskListByIdQuery(Guid UserId, Guid Id) : IRequest<TaskList?>;

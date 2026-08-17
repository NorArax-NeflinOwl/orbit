using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.DeleteTaskList;

public sealed record DeleteTaskListCommand(Guid UserId, Guid Id) : IRequest<bool>;

using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.MoveTaskItem;

[ClientAction(ClientActionCategory.Edit)]
public sealed record MoveTaskItemCommand(Guid UserId, Guid SourceTaskListId, Guid TaskItemId, Guid TargetTaskListId) : IRequest<EditOutcome>;

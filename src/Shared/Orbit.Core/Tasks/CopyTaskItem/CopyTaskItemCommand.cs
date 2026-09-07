using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.CopyTaskItem;

/// <summary>
/// Writes a second entry saying the same thing onto another list, leaving the first where it is. The
/// way round a move that cannot happen: an entry on a list somebody shared belongs to that list and
/// travels with it, so taking it out would take it from everybody else it was shared with - see
/// MoveTaskItemCommandHandler, which refuses exactly that and names this.
/// </summary>
[ClientAction(ClientActionCategory.Save)]
public sealed record CopyTaskItemCommand(Guid UserId, Guid SourceTaskListId, Guid TaskItemId, Guid TargetTaskListId)
    : IRequest<EditOutcome>;

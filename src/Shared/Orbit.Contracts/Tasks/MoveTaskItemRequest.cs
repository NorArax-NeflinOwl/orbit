namespace Orbit.Contracts.Tasks;

public sealed record MoveTaskItemRequest(Guid TargetTaskListId);

/// <summary>
/// Where to write a second entry saying the same thing, leaving the first where it is - the same body a
/// move takes, since the question is the same one. See CopyTaskItemCommand for what a copy carries.
/// </summary>
public sealed record CopyTaskItemRequest(Guid TargetTaskListId);

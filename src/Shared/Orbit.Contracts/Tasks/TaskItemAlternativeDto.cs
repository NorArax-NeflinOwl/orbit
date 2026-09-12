namespace Orbit.Contracts.Tasks;

/// <summary>
/// One way of getting an entry done - see Orbit.Core.Tasks.TaskItemAlternative. The same shape going
/// out and coming in.
/// </summary>
/// <param name="Description">What this way is. May be empty for a way that is a list, whose title then stands in.</param>
/// <param name="LinkedTaskListId">The list this way is, or null for a line ticked by hand.</param>
/// <param name="IsDone">
/// Whether this way has been taken. For a way that is a list the server works this out from the list
/// and ignores what was sent, the way it does for an entry standing for lists.
/// </param>
public sealed record TaskItemAlternativeDto(string Description, Guid? LinkedTaskListId = null, bool IsDone = false);

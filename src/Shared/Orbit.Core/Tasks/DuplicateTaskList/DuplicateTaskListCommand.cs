using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.DuplicateTaskList;

/// <summary>
/// A second list with the same entries on it. Only the owner's own - see DuplicateNoteCommand, which
/// says why.
/// </summary>
/// <param name="Name">What to call the copy, or null to keep the original's title - see Orbit.Contracts.DuplicateRequest.</param>
[ClientAction(ClientActionCategory.Save)]
public sealed record DuplicateTaskListCommand(Guid UserId, Guid Id, string? Name = null) : IRequest<Guid?>;

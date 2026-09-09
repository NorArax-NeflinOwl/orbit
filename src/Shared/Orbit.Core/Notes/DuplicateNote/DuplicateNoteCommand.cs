using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.DuplicateNote;

/// <summary>
/// A second note saying the same thing. Only the owner's own: a note reached through a share belongs to
/// somebody else, and copying it would put a note on this reader's page that its author never wrote
/// there - see INoteRepository.GetByIdAsync, which is scoped to the owner and is what answers null here.
/// </summary>
/// <param name="Name">What to call the copy, or null to keep the original's title - see Orbit.Contracts.DuplicateRequest.</param>
[ClientAction(ClientActionCategory.Save)]
public sealed record DuplicateNoteCommand(Guid UserId, Guid Id, string? Name = null) : IRequest<Guid?>;

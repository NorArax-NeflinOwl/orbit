using Orbit.Core.Abstractions;

namespace Orbit.Core.Places.DuplicatePlace;

/// <summary>
/// A second place saying the same thing, in the same spot. Mirrors DuplicateNoteCommand: only the
/// owner's own, and null back for an id that answers to nobody here.
/// </summary>
/// <param name="Name">What to call the copy, or null to keep the original's name - see Orbit.Contracts.DuplicateRequest.</param>
[ClientAction(ClientActionCategory.Save)]
public sealed record DuplicatePlaceCommand(Guid UserId, Guid Id, string? Name = null) : IRequest<Guid?>;

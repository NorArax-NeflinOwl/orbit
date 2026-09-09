using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.DuplicateInventory;

/// <summary>
/// A second storage holding the same things. Only the owner's own - see
/// Orbit.Core.Notes.DuplicateNote.DuplicateNoteCommand, which says why.
/// </summary>
/// <param name="Name">What to call the copy, or null to keep the original's name - see Orbit.Contracts.DuplicateRequest.</param>
[ClientAction(ClientActionCategory.Save)]
public sealed record DuplicateInventoryCommand(Guid UserId, Guid Id, string? Name = null) : IRequest<Guid?>;

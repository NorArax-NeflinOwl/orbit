using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.SetNotePinned;

/// <summary>Pins or unpins one note - see Note.SetPinned for why this is its own command rather than part of an update.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record SetNotePinnedCommand(Guid UserId, Guid NoteId, bool IsPinned) : IRequest<bool>;

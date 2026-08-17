using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.DeleteNote;

public sealed record DeleteNoteCommand(Guid UserId, Guid Id) : IRequest<bool>;

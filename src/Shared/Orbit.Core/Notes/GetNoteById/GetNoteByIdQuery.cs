using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNoteById;

public sealed record GetNoteByIdQuery(Guid UserId, Guid Id) : IRequest<Note?>;

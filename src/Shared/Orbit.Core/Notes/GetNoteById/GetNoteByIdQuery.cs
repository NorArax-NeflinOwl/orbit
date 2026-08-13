using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNoteById;

public sealed record GetNoteByIdQuery(Guid Id) : IRequest<Note?>;

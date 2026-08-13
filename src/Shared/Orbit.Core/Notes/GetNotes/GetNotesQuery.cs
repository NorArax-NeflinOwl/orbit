using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNotes;

public sealed record GetNotesQuery(Guid UserId) : IRequest<IReadOnlyList<Note>>;

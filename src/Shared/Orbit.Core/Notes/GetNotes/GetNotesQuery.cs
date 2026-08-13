using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNotes;

public sealed record GetNotesQuery : IRequest<IReadOnlyList<Note>>;

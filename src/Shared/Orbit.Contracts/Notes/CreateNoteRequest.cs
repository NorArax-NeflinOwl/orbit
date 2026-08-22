namespace Orbit.Contracts.Notes;

public sealed record CreateNoteRequest(string Title, IReadOnlyList<NoteContentLineDto> Content);

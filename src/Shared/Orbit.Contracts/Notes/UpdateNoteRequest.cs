namespace Orbit.Contracts.Notes;

public sealed record UpdateNoteRequest(string Title, IReadOnlyList<NoteContentLineDto> Content);

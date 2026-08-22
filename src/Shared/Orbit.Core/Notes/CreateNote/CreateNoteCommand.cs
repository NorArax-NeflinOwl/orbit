using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.CreateNote;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateNoteCommand(Guid UserId, string Title, IReadOnlyList<NoteContentLine> Content) : IRequest<Guid>;

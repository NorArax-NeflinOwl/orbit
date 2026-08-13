using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.CreateNote;

public sealed record CreateNoteCommand(string Title, string Content) : IRequest<Guid>;

using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.UpdateNote;

public sealed record UpdateNoteCommand(Guid Id, string Title, string Content) : IRequest<bool>;

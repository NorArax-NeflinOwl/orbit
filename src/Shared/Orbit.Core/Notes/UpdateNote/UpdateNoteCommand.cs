using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.UpdateNote;

[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateNoteCommand(Guid UserId, Guid Id, string Title, string Content) : IRequest<bool>;

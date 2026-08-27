using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.CreateNote;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateNoteCommand(
    Guid UserId, string Title, IReadOnlyList<NoteContentLine> Content, bool IsPrivate, EncryptedPayload? EncryptedContent,
    ItemPriority Priority = ItemPriority.Normal) : IRequest<Guid>;

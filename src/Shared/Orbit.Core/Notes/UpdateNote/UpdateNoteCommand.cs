using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.UpdateNote;

[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateNoteCommand(
    Guid UserId, Guid Id, string Title, IReadOnlyList<NoteContentLine> Content, bool IsPrivate, EncryptedPayload? EncryptedContent,
    ItemPriority Priority = ItemPriority.Normal,
    /// <summary>Null leaves the stored tags alone - see UpdateNoteRequest.Tags. An empty list clears them.</summary>
    IReadOnlyList<string>? Tags = null) : IRequest<EditOutcome>;

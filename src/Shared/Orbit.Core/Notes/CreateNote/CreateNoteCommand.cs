using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.CreateNote;

[ClientAction(ClientActionCategory.Save)]
public sealed record CreateNoteCommand(
    Guid UserId, string Title, IReadOnlyList<NoteContentLine> Content, bool IsPrivate, EncryptedPayload? EncryptedContent,
    ItemPriority Priority = ItemPriority.Normal,
    /// <summary>Where to file it, or null for the built-in folder its privacy decides - see Orbit.Core.Folders.BuiltInFolder.</summary>
    Guid? FolderId = null,
    /// <summary>The words it is tagged with - see Note.Tags.</summary>
    IReadOnlyList<string>? Tags = null) : IRequest<Guid>;

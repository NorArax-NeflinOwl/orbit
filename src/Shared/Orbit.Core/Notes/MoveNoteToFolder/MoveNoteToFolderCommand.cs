using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.MoveNoteToFolder;

/// <summary>
/// Files one note under <paramref name="FolderId"/>, or under none when it is null - which puts it back
/// in whichever built-in folder its privacy says (see Orbit.Core.Folders.BuiltInFolder).
///
/// Its own command rather than a field on the update, for the reason Note.MoveToFolder gives: an update
/// replaces the whole note, so null there would have to mean "leave it alone" and there would be no way
/// left to say "take it out of the folder".
/// </summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record MoveNoteToFolderCommand(Guid UserId, Guid NoteId, Guid? FolderId) : IRequest<bool>;

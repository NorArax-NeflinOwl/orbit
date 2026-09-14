using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.UpdateNote;

[ClientAction(ClientActionCategory.Edit)]
public sealed record UpdateNoteCommand(
    Guid UserId, Guid Id, string Title, IReadOnlyList<NoteContentLine> Content, bool IsPrivate, EncryptedPayload? EncryptedContent,
    ItemPriority Priority = ItemPriority.Normal,
    /// <summary>Null leaves the stored tags alone - see UpdateNoteRequest.Tags. An empty list clears them.</summary>
    IReadOnlyList<string>? Tags = null,
    /// <summary>
    /// The pictures the note still holds, by id - every other picture of the note is swept. Null means
    /// the client said nothing (a build that predates pictures), and then nothing is swept. See
    /// NotePictureSweeper.RemoveUnnamedAsync for why the client has to say: a private note's lines are
    /// sealed, and the server cannot read which pictures they name.
    /// </summary>
    IReadOnlyCollection<Guid>? KeptPictureIds = null) : IRequest<EditOutcome>;

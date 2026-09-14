using Orbit.Contracts;
namespace Orbit.Contracts.Notes;

/// <summary>
/// IsPrivate marks a note only its owner can read: Title and Content then travel empty and the real
/// values are sealed inside EncryptedContent, which the browser fills in and the server never opens.
/// </summary>
/// <param name="PictureIds">
/// The pictures the note still holds, by id; every other picture of the note is taken away with this
/// save. Null means "not provided" - a client written before pictures existed - and takes nothing away.
/// Said by the client because a private note's lines are sealed, and the server cannot read which
/// pictures they name.
/// </param>
public sealed record UpdateNoteRequest(
    string Title, IReadOnlyList<NoteContentLineDto> Content, bool IsPrivate = false, EncryptedContentDto? EncryptedContent = null,
    string Priority = "Normal",
    /// <summary>
    /// The words it is tagged with - see NoteDto.Tags. <b>Null means "not provided"</b> and leaves the
    /// stored tags alone, which is what a client written before tags existed sends - an installed phone
    /// saving a note must not untag it. An empty list means "none", and clears them.
    /// </summary>
    IReadOnlyList<string>? Tags = null,
    IReadOnlyList<Guid>? PictureIds = null);

using Orbit.Contracts;
namespace Orbit.Contracts.Notes;

/// <summary>
/// IsPrivate marks a note only its owner can read: Title and Content then travel empty and the real
/// values are sealed inside EncryptedContent, which the browser fills in and the server never opens.
/// </summary>
public sealed record CreateNoteRequest(
    string Title, IReadOnlyList<NoteContentLineDto> Content, bool IsPrivate = false, EncryptedContentDto? EncryptedContent = null,
    string Priority = "Normal",
    /// <summary>
    /// The folder to file it under, or null to leave it in the built-in one its privacy decides - see
    /// Orbit.Core.Folders.BuiltInFolder. Only on the way in: moving an existing note is its own request
    /// (MoveToFolderRequest), for the reason that one gives.
    /// </summary>
    Guid? FolderId = null,
    /// <summary>The words it is tagged with - see NoteDto.Tags. Null and empty both mean none.</summary>
    IReadOnlyList<string>? Tags = null);

using Orbit.Contracts;
namespace Orbit.Contracts.Notes;

/// <summary>
/// IsPrivate marks a note only its owner can read: Title and Content then travel empty and the real
/// values are sealed inside EncryptedContent, which the browser fills in and the server never opens.
/// </summary>
public sealed record CreateNoteRequest(
    string Title, IReadOnlyList<NoteContentLineDto> Content, bool IsPrivate = false, EncryptedContentDto? EncryptedContent = null);

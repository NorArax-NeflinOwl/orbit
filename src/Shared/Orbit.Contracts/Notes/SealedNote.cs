namespace Orbit.Contracts.Notes;

/// <summary>
/// Everything a private note hides from the server, as one payload: what gets serialized and sealed
/// into <see cref="Orbit.Contracts.EncryptedContentDto"/>, and what comes back out of it.
///
/// Shared between the clients rather than declared in each, because the bytes have to match. A phone
/// that spelled this <c>Text</c> where the browser spells it <c>Content</c> would seal notes the
/// browser could no longer read, and neither side would find out until somebody opened one on the
/// other device.
/// </summary>
public sealed record SealedNote(string Title, IReadOnlyList<NoteContentLineDto> Content);

using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNotePicture;

/// <summary>The bytes of one picture, for a reader who may see the note - its owner, or somebody it is shared with.</summary>
public sealed record GetNotePictureQuery(Guid UserId, Guid NoteId, Guid PictureId) : IRequest<NotePictureContent?>;

/// <summary>A picture's bytes and what they are. The stream is the caller's to dispose once it has been sent.</summary>
public sealed record NotePictureContent(NotePicture Picture, Stream Content);

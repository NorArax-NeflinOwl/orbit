using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.DeleteNotePicture;

/// <summary>One picture taken away from a note - the row and the bytes. False when there was no such picture to take.</summary>
[ClientAction(ClientActionCategory.Edit)]
public sealed record DeleteNotePictureCommand(Guid UserId, Guid NoteId, Guid PictureId) : IRequest<bool>;

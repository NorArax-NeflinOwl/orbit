using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.DeleteNotePicture;

public sealed class DeleteNotePictureCommandHandler : IRequestHandler<DeleteNotePictureCommand, bool>
{
    private readonly NoteAccessResolver _noteAccessResolver;
    private readonly INotePictureRepository _pictures;
    private readonly INotePictureStore _store;

    public DeleteNotePictureCommandHandler(NoteAccessResolver noteAccessResolver, INotePictureRepository pictures, INotePictureStore store)
    {
        _noteAccessResolver = noteAccessResolver;
        _pictures = pictures;
        _store = store;
    }

    public async Task<bool> HandleAsync(DeleteNotePictureCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteAccessResolver.ResolveAsync(request.UserId, request.NoteId, cancellationToken);
        if (note is null || !note.AccessLevel.AllowsEditing())
        {
            return false;
        }

        var picture = await _pictures.GetByIdAsync(request.PictureId, cancellationToken);
        if (picture is null || picture.NoteId != note.Id)
        {
            return false;
        }

        await NotePictureSweeper.RemoveAsync(picture, _pictures, _store, cancellationToken);
        return true;
    }
}

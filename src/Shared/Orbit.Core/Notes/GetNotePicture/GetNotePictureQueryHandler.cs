using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.GetNotePicture;

public sealed class GetNotePictureQueryHandler : IRequestHandler<GetNotePictureQuery, NotePictureContent?>
{
    private readonly NoteAccessResolver _noteAccessResolver;
    private readonly INotePictureRepository _pictures;
    private readonly INotePictureStore _store;

    public GetNotePictureQueryHandler(NoteAccessResolver noteAccessResolver, INotePictureRepository pictures, INotePictureStore store)
    {
        _noteAccessResolver = noteAccessResolver;
        _pictures = pictures;
        _store = store;
    }

    /// <summary>
    /// Null for anything that is not "this reader may see this picture of this note": a note they cannot
    /// see, a picture that is not that note's, or bytes the store no longer has. One answer for all of
    /// them, so the API's 404 says nothing about which.
    /// </summary>
    public async Task<NotePictureContent?> HandleAsync(GetNotePictureQuery request, CancellationToken cancellationToken)
    {
        var note = await _noteAccessResolver.ResolveAsync(request.UserId, request.NoteId, cancellationToken);
        if (note is null)
        {
            return null;
        }

        var picture = await _pictures.GetByIdAsync(request.PictureId, cancellationToken);
        if (picture is null || picture.NoteId != note.Id)
        {
            return null;
        }

        var content = await _store.OpenAsync(picture.Id, cancellationToken);
        return content is null ? null : new NotePictureContent(picture, content);
    }
}

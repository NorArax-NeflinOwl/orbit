using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.GetNotePicture;

namespace Orbit.Core.Sharing.GetPublicSharedPicture;

public sealed class GetPublicSharedPictureQueryHandler : IRequestHandler<GetPublicSharedPictureQuery, NotePictureContent?>
{
    private readonly IPublicShareLinkRepository _links;
    private readonly INotePictureRepository _pictures;
    private readonly INotePictureStore _store;

    public GetPublicSharedPictureQueryHandler(IPublicShareLinkRepository links, INotePictureRepository pictures, INotePictureStore store)
    {
        _links = links;
        _pictures = pictures;
        _store = store;
    }

    /// <summary>
    /// The token is the whole of the access check, as it is for the item itself: a live link to a note,
    /// and a picture that is that note's and not sealed. Anything else is nothing, for the reason
    /// GetNotePictureQueryHandler gives.
    /// </summary>
    public async Task<NotePictureContent?> HandleAsync(GetPublicSharedPictureQuery request, CancellationToken cancellationToken)
    {
        var link = await _links.GetByTokenAsync(request.Token, cancellationToken);
        if (link is null || link.IsRevoked || link.ItemType != SharedItemType.Note)
        {
            return null;
        }

        var picture = await _pictures.GetByIdAsync(request.PictureId, cancellationToken);
        if (picture is null || picture.NoteId != link.ItemId || picture.IsSealed)
        {
            return null;
        }

        var content = await _store.OpenAsync(picture.Id, cancellationToken);
        return content is null ? null : new NotePictureContent(picture, content);
    }
}

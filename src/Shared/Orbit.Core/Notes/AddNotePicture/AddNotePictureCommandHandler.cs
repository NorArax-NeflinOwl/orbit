using Orbit.Core.Abstractions;

namespace Orbit.Core.Notes.AddNotePicture;

public sealed class AddNotePictureCommandHandler : IRequestHandler<AddNotePictureCommand, AddNotePictureOutcome>
{
    private readonly NoteAccessResolver _noteAccessResolver;
    private readonly INotePictureRepository _pictures;
    private readonly INotePictureStore _store;

    public AddNotePictureCommandHandler(NoteAccessResolver noteAccessResolver, INotePictureRepository pictures, INotePictureStore store)
    {
        _noteAccessResolver = noteAccessResolver;
        _pictures = pictures;
        _store = store;
    }

    /// <summary>
    /// The order is the order the refusals cost: who may change the note, then whether the picture may
    /// be stored at all, then how much the note already holds - and the bytes are written only once every
    /// question has been answered, so a refused upload leaves nothing behind in the store.
    ///
    /// A private note's picture must arrive sealed. That is the same invariant Note.Create holds for the
    /// note itself (EnsureSealedWhenPrivate), asked here for the picture: a screen's good manners are not
    /// where a private picture stays private. A public note's picture arriving sealed is refused too -
    /// nothing could ever draw it for a reader the note is shared with.
    ///
    /// The row is written after the bytes, so a crash between the two leaves a blob nobody counts rather
    /// than a row whose bytes are missing; the store's Delete tolerates the other order for a reason.
    /// </summary>
    public async Task<AddNotePictureOutcome> HandleAsync(AddNotePictureCommand request, CancellationToken cancellationToken)
    {
        var note = await _noteAccessResolver.ResolveAsync(request.UserId, request.NoteId, cancellationToken);
        if (note is null)
        {
            return AddNotePictureOutcome.NotFound;
        }

        if (!note.AccessLevel.AllowsEditing())
        {
            return AddNotePictureOutcome.ReadOnly;
        }

        if (note.IsPrivate != request.IsSealed)
        {
            return AddNotePictureOutcome.MustBeSealed;
        }

        if (request.SizeBytes > NotePictureLimits.MaximumBytesPerPicture)
        {
            return AddNotePictureOutcome.TooLarge;
        }

        var held = await _pictures.TotalBytesForNoteAsync(request.NoteId, cancellationToken);
        if (held + request.SizeBytes > NotePictureLimits.MaximumBytesPerNote)
        {
            return AddNotePictureOutcome.TooLarge;
        }

        // The row records what the store actually took, not what the request's header claimed - a
        // caller that is not the app could say one thing and send another, and the count against the
        // note's 50 MB has to be the truth.
        var pictureId = Guid.NewGuid();
        var written = await _store.WriteAsync(pictureId, request.Content, cancellationToken);
        if (written == 0 || written > NotePictureLimits.MaximumBytesPerPicture || held + written > NotePictureLimits.MaximumBytesPerNote)
        {
            await _store.DeleteAsync(pictureId, cancellationToken);
            return AddNotePictureOutcome.TooLarge;
        }

        var picture = NotePicture.Create(pictureId, note.Id, note.UserId, written, request.ContentType, request.IsSealed);
        await _pictures.AddAsync(picture, cancellationToken);
        return AddNotePictureOutcome.Added(picture);
    }
}

namespace Orbit.Core.Notes;

/// <summary>
/// Takes pictures away - the bytes first, then the row - wherever a note stops needing them: one on its
/// own, every one a save no longer names, and all of them when the note goes. The row goes last so that
/// a failure between the two leaves a row pointing at nothing (which the next sweep finds) rather than
/// bytes nobody counts.
/// </summary>
public static class NotePictureSweeper
{
    public static async Task RemoveAsync(
        NotePicture picture, INotePictureRepository pictures, INotePictureStore store, CancellationToken cancellationToken)
    {
        await store.DeleteAsync(picture.Id, cancellationToken);
        await pictures.DeleteAsync(picture.Id, cancellationToken);
    }

    /// <summary>
    /// Every picture of the note that <paramref name="keptPictureIds"/> does not name, taken away. This
    /// is how a picture whose line was deleted goes: the client says which ids the note still holds when
    /// it saves - it has to, because a private note's lines are sealed and the server cannot read which
    /// pictures they name. Null means the client said nothing (a build that predates pictures), and then
    /// nothing is swept rather than everything.
    /// </summary>
    public static async Task RemoveUnnamedAsync(
        Guid noteId, IReadOnlyCollection<Guid>? keptPictureIds,
        INotePictureRepository pictures, INotePictureStore store, CancellationToken cancellationToken)
    {
        if (keptPictureIds is null)
        {
            return;
        }

        foreach (var picture in await pictures.GetForNoteAsync(noteId, cancellationToken))
        {
            if (!keptPictureIds.Contains(picture.Id))
            {
                await RemoveAsync(picture, pictures, store, cancellationToken);
            }
        }
    }

    public static async Task RemoveAllAsync(
        Guid noteId, INotePictureRepository pictures, INotePictureStore store, CancellationToken cancellationToken)
    {
        foreach (var picture in await pictures.GetForNoteAsync(noteId, cancellationToken))
        {
            await RemoveAsync(picture, pictures, store, cancellationToken);
        }
    }
}

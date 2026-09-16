using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Orbit.Core.Notes.AddNotePicture;
using Orbit.Core.Notes.DeleteNote;
using Orbit.Core.Notes.UpdateNote;
using Xunit;

namespace Orbit.Api.Tests.Notes;

/// <summary>
/// A picture kept for a note - the rules in AddNotePictureCommandHandler and what a save and a delete do
/// to the pictures a note held (NotePictureSweeper). The limits and the sealing rule are the two that
/// were settled with the user, so they are the two pinned down hardest.
/// </summary>
public sealed class AddNotePictureCommandHandlerTests
{
    private readonly InMemoryNoteRepository _notes = new();
    private readonly InMemoryNoteShareRepository _shares = new();
    private readonly InMemoryUserRepository _users = new();
    private readonly InMemoryNotePictureRepository _pictures = new();
    private readonly InMemoryNotePictureStore _store = new();
    private readonly Guid _ownerId = Guid.NewGuid();

    private NoteAccessResolver Resolver => new(_notes, _shares, _users);

    private AddNotePictureCommandHandler Handler => new(Resolver, _pictures, _store);

    private async Task<Note> ANoteAsync(bool isPrivate = false)
    {
        var note = isPrivate
            ? Note.Create(_ownerId, string.Empty, [], isPrivate: true, new EncryptedPayload("sealed", "nonce"))
            : Note.Create(_ownerId, "Shopping", [NoteContentLine.PlainText("milk")]);
        await _notes.AddAsync(note, CancellationToken.None);
        return note;
    }

    private static AddNotePictureCommand Upload(Guid userId, Guid noteId, int sizeBytes, bool isSealed = false)
        => new(userId, noteId, new MemoryStream(new byte[sizeBytes]), sizeBytes, isSealed ? null : "image/png", isSealed);

    [Fact]
    public async Task A_picture_is_kept_with_what_the_store_actually_took()
    {
        var note = await ANoteAsync();

        var outcome = await Handler.HandleAsync(Upload(_ownerId, note.Id, 1000), CancellationToken.None);

        Assert.Equal(AddNotePictureOutcomeKind.Added, outcome.Kind);
        Assert.Equal(1000, outcome.Picture!.SizeBytes);
        Assert.Equal("image/png", outcome.Picture.ContentType);
        Assert.Equal(1000, _store.Bytes[outcome.Picture.Id].Length);
        Assert.Equal(1000, await _pictures.TotalBytesForNoteAsync(note.Id, CancellationToken.None));
    }

    /// <summary>50 MB across the note, not per picture: the one that takes the note past it is refused, and nothing of it is left behind.</summary>
    [Fact]
    public async Task A_picture_that_takes_the_note_past_50_MB_is_refused_and_leaves_nothing()
    {
        var note = await ANoteAsync();
        await _pictures.AddAsync(
            NotePicture.Create(Guid.NewGuid(), note.Id, _ownerId, NotePictureLimits.MaximumBytesPerNote - 10, "image/png", isSealed: false),
            CancellationToken.None);

        var outcome = await Handler.HandleAsync(Upload(_ownerId, note.Id, 11), CancellationToken.None);

        Assert.Equal(AddNotePictureOutcomeKind.TooLarge, outcome.Kind);
        Assert.Empty(_store.Bytes);
        Assert.Single(await _pictures.GetForNoteAsync(note.Id, CancellationToken.None));
    }

    /// <summary>The header is not believed: the store's count is what the note is charged, so a header that lied about a size is caught after the write.</summary>
    [Fact]
    public async Task A_header_that_understates_the_size_does_not_get_past_the_limit()
    {
        var note = await ANoteAsync();
        await _pictures.AddAsync(
            NotePicture.Create(Guid.NewGuid(), note.Id, _ownerId, NotePictureLimits.MaximumBytesPerNote - 10, "image/png", isSealed: false),
            CancellationToken.None);
        var lying = new AddNotePictureCommand(_ownerId, note.Id, new MemoryStream(new byte[100]), SizeBytes: 5, "image/png", IsSealed: false);

        var outcome = await Handler.HandleAsync(lying, CancellationToken.None);

        Assert.Equal(AddNotePictureOutcomeKind.TooLarge, outcome.Kind);
        Assert.Empty(_store.Bytes);
    }

    /// <summary>A private note's picture must arrive sealed - the invariant Note.Create holds for the note, asked here for the picture.</summary>
    [Fact]
    public async Task A_private_note_s_picture_arriving_in_the_clear_is_refused()
    {
        var note = await ANoteAsync(isPrivate: true);

        var outcome = await Handler.HandleAsync(Upload(_ownerId, note.Id, 100, isSealed: false), CancellationToken.None);

        Assert.Equal(AddNotePictureOutcomeKind.MustBeSealed, outcome.Kind);
        Assert.Empty(_store.Bytes);
    }

    [Fact]
    public async Task A_sealed_picture_of_a_private_note_is_kept_with_no_content_type_beside_it()
    {
        var note = await ANoteAsync(isPrivate: true);

        var outcome = await Handler.HandleAsync(Upload(_ownerId, note.Id, 100, isSealed: true), CancellationToken.None);

        Assert.Equal(AddNotePictureOutcomeKind.Added, outcome.Kind);
        Assert.True(outcome.Picture!.IsSealed);
        Assert.Null(outcome.Picture.ContentType);
    }

    /// <summary>And the other way round: a public note's picture arriving sealed could never be drawn for a reader it is shared with.</summary>
    [Fact]
    public async Task A_public_note_s_picture_arriving_sealed_is_refused()
    {
        var note = await ANoteAsync();

        var outcome = await Handler.HandleAsync(Upload(_ownerId, note.Id, 100, isSealed: true), CancellationToken.None);

        Assert.Equal(AddNotePictureOutcomeKind.MustBeSealed, outcome.Kind);
    }

    [Fact]
    public async Task Somebody_else_s_note_is_not_found()
    {
        var note = await ANoteAsync();

        var outcome = await Handler.HandleAsync(Upload(Guid.NewGuid(), note.Id, 100), CancellationToken.None);

        Assert.Equal(AddNotePictureOutcomeKind.NotFound, outcome.Kind);
        Assert.Empty(_store.Bytes);
    }

    [Fact]
    public async Task A_reader_the_note_is_only_shared_to_read_may_not_add_one()
    {
        var note = await ANoteAsync();
        var readerId = Guid.NewGuid();
        var share = NoteShare.Create(note.Id, _ownerId, readerId, ShareAccessLevel.ReadOnly);
        share.MarkAccepted();
        await _shares.AddAsync(share, CancellationToken.None);

        var outcome = await Handler.HandleAsync(Upload(readerId, note.Id, 100), CancellationToken.None);

        Assert.Equal(AddNotePictureOutcomeKind.ReadOnly, outcome.Kind);
    }

    /// <summary>
    /// The save says which pictures the note still holds, and the rest go - bytes and rows. The client
    /// has to say, because a private note's lines are sealed and the server cannot read them.
    /// </summary>
    [Fact]
    public async Task A_save_sweeps_the_pictures_it_no_longer_names()
    {
        var note = await ANoteAsync();
        var kept = await Handler.HandleAsync(Upload(_ownerId, note.Id, 100), CancellationToken.None);
        var dropped = await Handler.HandleAsync(Upload(_ownerId, note.Id, 100), CancellationToken.None);
        var update = new UpdateNoteCommandHandler(Resolver, _notes, _pictures, _store);

        await update.HandleAsync(
            new UpdateNoteCommand(
                _ownerId, note.Id, "Shopping", [NoteContentLine.PlainText("milk")], IsPrivate: false, EncryptedContent: null,
                KeptPictureIds: [kept.Picture!.Id]),
            CancellationToken.None);

        Assert.Equal([kept.Picture.Id], _store.Bytes.Keys);
        Assert.Equal([kept.Picture.Id], (await _pictures.GetForNoteAsync(note.Id, CancellationToken.None)).Select(picture => picture.Id));
        Assert.Null(await _pictures.GetByIdAsync(dropped.Picture!.Id, CancellationToken.None));
    }

    /// <summary>A client that says nothing - a build that predates pictures - sweeps nothing rather than everything.</summary>
    [Fact]
    public async Task A_save_that_names_no_pictures_sweeps_none()
    {
        var note = await ANoteAsync();
        var picture = await Handler.HandleAsync(Upload(_ownerId, note.Id, 100), CancellationToken.None);
        var update = new UpdateNoteCommandHandler(Resolver, _notes, _pictures, _store);

        await update.HandleAsync(
            new UpdateNoteCommand(_ownerId, note.Id, "Shopping", [NoteContentLine.PlainText("milk")], IsPrivate: false, EncryptedContent: null),
            CancellationToken.None);

        Assert.NotNull(await _pictures.GetByIdAsync(picture.Picture!.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Deleting_the_note_takes_its_pictures_with_it()
    {
        var note = await ANoteAsync();
        await Handler.HandleAsync(Upload(_ownerId, note.Id, 100), CancellationToken.None);
        var delete = new DeleteNoteCommandHandler(_notes, _shares, new InMemorySyncTombstoneRepository(), _pictures, _store);

        await delete.HandleAsync(new DeleteNoteCommand(_ownerId, note.Id), CancellationToken.None);

        Assert.Empty(_store.Bytes);
        Assert.Empty(await _pictures.GetForNoteAsync(note.Id, CancellationToken.None));
    }
}

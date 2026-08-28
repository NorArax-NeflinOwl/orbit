using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventory;
using Orbit.Core.Notes;
using Orbit.Core.Notes.GetNotes;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Notes;

/// <summary>
/// Covers a private item arriving with nothing actually sealed inside it. A request whose
/// encryptedContent object is present but whose members didn't bind - a client sending the wrong
/// property names - used to be accepted, and from then on every read of that owner's list threw on the
/// bad row and took the whole list down with it.
/// </summary>
public sealed class MalformedEncryptedContentTests
{
    [Fact]
    public void A_payload_with_no_ciphertext_is_refused()
        => Assert.Throws<InvalidRequestException>(() => new EncryptedPayload(null!, "bm9uY2U="));

    [Fact]
    public void A_payload_with_no_nonce_is_refused()
        => Assert.Throws<InvalidRequestException>(() => new EncryptedPayload("c2VhbGVk", null!));

    [Fact]
    public void A_payload_of_blanks_is_refused()
    {
        // Empty strings are what an unbound member leaves behind just as often as nulls do, and a row
        // written from them is exactly as unopenable.
        Assert.Throws<InvalidRequestException>(() => new EncryptedPayload("   ", "bm9uY2U="));
        Assert.Throws<InvalidRequestException>(() => new EncryptedPayload(string.Empty, "bm9uY2U="));
    }

    [Fact]
    public void A_private_note_with_no_payload_at_all_is_still_refused()
        => Assert.Throws<InvalidRequestException>(
            () => Note.Create(Guid.NewGuid(), string.Empty, [], isPrivate: true, encryptedContent: null));

    [Fact]
    public void A_private_task_list_with_no_payload_is_refused()
        => Assert.Throws<InvalidRequestException>(
            () => TaskList.Create(Guid.NewGuid(), string.Empty, [], isPrivate: true, encryptedContent: null));

    [Fact]
    public void A_private_warehouse_with_no_payload_is_refused()
        => Assert.Throws<InvalidRequestException>(
            () => Warehouse.Create(Guid.NewGuid(), string.Empty, isPrivate: true, encryptedContent: null));

    [Fact]
    public void Turning_a_note_private_with_no_payload_is_refused()
    {
        var note = Note.Create(Guid.NewGuid(), "Shopping list", [NoteContentLine.PlainText("Milk")]);

        Assert.Throws<InvalidRequestException>(() => note.Update(string.Empty, [], isPrivate: true, encryptedContent: null, note.Priority));
    }

    [Fact]
    public void A_properly_sealed_private_note_is_still_accepted()
    {
        // The control: without it, everything above could equally be satisfied by refusing every private
        // note there is.
        var note = Note.Create(
            Guid.NewGuid(), string.Empty, [], isPrivate: true, new EncryptedPayload("c2VhbGVk", "bm9uY2U="));

        Assert.True(note.IsPrivate);
        Assert.Equal("c2VhbGVk", note.EncryptedContent!.Ciphertext);
    }

    [Fact]
    public void A_row_already_stored_broken_still_rebuilds()
    {
        // Rebuilding deliberately skips the write-time check - see Note.EnsureSealedWhenPrivate. It
        // rebuilds as what it is: private, and unopenable.
        var note = RebuildBrokenPrivateNote();

        Assert.True(note.IsPrivate);
        Assert.Null(note.EncryptedContent);
        Assert.Equal(string.Empty, note.Title);
    }

    [Fact]
    public async Task A_list_containing_an_already_broken_row_still_loads()
    {
        var noteRepository = new InMemoryNoteRepository();
        var ownerId = Guid.NewGuid();
        await noteRepository.AddAsync(Note.Create(ownerId, "A good note", [NoteContentLine.PlainText("Milk")]), CancellationToken.None);
        await noteRepository.AddAsync(RebuildBrokenPrivateNote(ownerId), CancellationToken.None);

        var accessResolver = new NoteAccessResolver(noteRepository, new InMemoryNoteShareRepository(), new InMemoryUserRepository());
        var notes = await new GetNotesQueryHandler(accessResolver).HandleAsync(new GetNotesQuery(ownerId), CancellationToken.None);

        // This is the whole point of rebuilding tolerantly: one unopenable row must not take the good
        // ones down with it.
        Assert.Equal(2, notes.Count);
        Assert.Contains(notes, note => note.Title == "A good note");
    }

    /// <summary>A note in the state the old write path could leave behind: marked private, with nothing sealed inside it.</summary>
    private static Note RebuildBrokenPrivateNote(Guid? ownerId = null)
        => Note.FromPersistence(
            Guid.NewGuid(), ownerId ?? Guid.NewGuid(), string.Empty, [], isPrivate: true, encryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null);
}

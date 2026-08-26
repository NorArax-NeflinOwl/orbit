using Orbit.Core.Abstractions;
using Orbit.Core.Notes;
using Xunit;

namespace Orbit.Api.Tests.Notes;

/// <summary>
/// Covers what counts as a note worth having. A note with nothing in it shows up in the list as a blank
/// row that says nothing about itself, and can only have been made by accident.
/// </summary>
public sealed class EmptyNoteTests
{
    [Fact]
    public void A_note_with_no_title_and_no_lines_is_refused()
        => Assert.Throws<InvalidRequestException>(() => Note.Create(Guid.NewGuid(), string.Empty, []));

    [Fact]
    public void A_note_of_nothing_but_whitespace_is_refused()
    {
        // Spaces are how an empty note arrives in practice: something was typed and then deleted.
        Assert.Throws<InvalidRequestException>(
            () => Note.Create(Guid.NewGuid(), "   ", [NoteContentLine.PlainText("  ")]));
    }

    [Fact]
    public void A_title_on_its_own_is_a_whole_note()
    {
        // "Dentist on Tuesday" needs no body, and demanding one would be inventing a rule nobody asked for.
        var note = Note.Create(Guid.NewGuid(), "Dentist on Tuesday", []);

        Assert.Equal("Dentist on Tuesday", note.Title);
    }

    [Fact]
    public void A_line_on_its_own_is_a_whole_note()
    {
        var note = Note.Create(Guid.NewGuid(), string.Empty, [NoteContentLine.PlainText("Milk")]);

        Assert.Equal("Milk", Assert.Single(note.Content).Text);
    }

    [Fact]
    public void A_checklist_item_counts_as_something_written()
        => Note.Create(Guid.NewGuid(), string.Empty, [new NoteContentLine("Buy milk", IsChecklistItem: true, IsChecked: false)]);

    [Fact]
    public void A_private_note_is_exempt()
    {
        // Its readable fields travel empty by design - what it says is inside the sealed payload, where
        // this check cannot look.
        var note = Note.Create(
            Guid.NewGuid(), string.Empty, [], isPrivate: true, new EncryptedPayload("c2VhbGVk", "bm9uY2U="));

        Assert.True(note.IsPrivate);
    }

    [Fact]
    public void Emptying_an_existing_note_is_refused_too()
    {
        var note = Note.Create(Guid.NewGuid(), "Shopping list", [NoteContentLine.PlainText("Milk")]);

        // Otherwise the rule would only hold for as long as it took to save once and edit it back down.
        Assert.Throws<InvalidRequestException>(() => note.Update(string.Empty, [], isPrivate: false, encryptedContent: null));
    }

    [Fact]
    public void An_already_stored_empty_note_still_rebuilds()
    {
        // Same reasoning as the sealed-payload check: this rule stops a bad write, and throwing while
        // reading an old row would take the reader's whole list down with it.
        var note = Note.FromPersistence(
            Guid.NewGuid(), Guid.NewGuid(), string.Empty, [], isPrivate: false, encryptedContent: null,
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow,
            lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null);

        Assert.Equal(string.Empty, note.Title);
    }
}

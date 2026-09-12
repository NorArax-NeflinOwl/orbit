using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Tags;

/// <summary>
/// A note's tags on the phone: written here, sent with the note, sealed with a private one, and never
/// emptied by a note this phone held before it knew about tags. The same code carries a list's - see
/// LocalTaskListRepository and TaskListSynchronizer, which follow these lines one for one.
/// </summary>
public sealed class NoteTagsSyncTests : IDisposable
{
    private static readonly IReadOnlyList<NoteContentLineDto> Lines = [new("milk", false, false)];

    private readonly LocalStore _store = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-09-11T10:00:00Z"));
    private readonly FakeNotesServer _server;
    private readonly LocalNoteRepository _notes;
    private readonly NoteSynchronizer _synchronizer;

    public NoteTagsSyncTests()
    {
        _server = new FakeNotesServer(_clock);
        _notes = new LocalNoteRepository(
            _store, _clock, FixedNetworkStatus.Online, PrivateContent.HoldingAKeyFor(Guid.NewGuid()));
        _synchronizer = new NoteSynchronizer(
            _store, new NotesClient(_server.ToHttpClient()), _clock, new SyncGate(),
            NullLogger<NoteSynchronizer>.Instance);
    }

    public void Dispose()
    {
        _store.Dispose();
        _server.Dispose();
    }

    [Fact]
    public async Task A_notes_tags_are_sent_with_it()
    {
        var note = await _notes.CreateAsync("Groceries", Lines);
        await _notes.UpdateAsync(note.LocalId, new NoteContent("Groceries", Lines, "Normal", Tags: ["work", "Work", "home"]));

        await _synchronizer.SynchroniseAsync();

        Assert.Equal(["work", "home"], Assert.Single(_server.Notes).AllTags);
    }

    /// <summary>
    /// A private note's tags go into its seal and nowhere readable - on the phone as on the server - and come
    /// back out when the note is opened here.
    /// </summary>
    [Fact]
    public async Task A_private_notes_tags_are_sealed_rather_than_kept_readable()
    {
        var note = await _notes.CreateAsync("Bank", Lines);

        await _notes.UpdateAsync(
            note.LocalId, new NoteContent("Bank", Lines, "Normal", IsPrivate: true, Tags: ["money"]));

        using (var dbContext = _store.CreateDbContext())
        {
            Assert.Empty(dbContext.Notes.Single().Tags!);
        }

        Assert.Equal(["money"], (await _notes.FindAsync(note.LocalId))!.Tags);
    }

    /// <summary>And a save that says nothing about tags keeps the sealed ones, rather than sealing none.</summary>
    [Fact]
    public async Task A_private_note_saved_without_a_word_about_tags_keeps_its_sealed_ones()
    {
        var note = await _notes.CreateAsync("Bank", Lines);
        await _notes.UpdateAsync(
            note.LocalId, new NoteContent("Bank", Lines, "Normal", IsPrivate: true, Tags: ["money"]));

        await _notes.UpdateAsync(note.LocalId, new NoteContent("Bank account", Lines, "Normal", IsPrivate: true));

        Assert.Equal(["money"], (await _notes.FindAsync(note.LocalId))!.Tags);
    }

    /// <summary>
    /// A note this phone held before tags existed has a null in the column - "not known" - and pushes it as
    /// null, which the server reads as "not provided". Pushing an empty list would have emptied the tags a
    /// browser had put on it since.
    /// </summary>
    [Fact]
    public async Task A_note_held_since_before_tags_does_not_empty_the_ones_written_elsewhere()
    {
        var note = await _notes.CreateAsync("Groceries", Lines);
        await _synchronizer.SynchroniseAsync();
        var onTheServer = Assert.Single(_server.Notes);
        _server.ReplaceForTest(onTheServer with { Tags = ["work"] });
        using (var dbContext = _store.CreateDbContext())
        {
            dbContext.Notes.Single().Tags = null;
            dbContext.SaveChanges();
        }

        await _notes.UpdateAsync(note.LocalId, new NoteContent("Groceries for Friday", Lines, "Normal"));
        await _synchronizer.SynchroniseAsync();

        var saved = Assert.Single(_server.Notes);
        Assert.Equal("Groceries for Friday", saved.Title);
        Assert.Equal(["work"], saved.AllTags);
    }
}

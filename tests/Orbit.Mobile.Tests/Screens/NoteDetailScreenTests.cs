using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;
using Orbit.Mobile.Chat;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The screen a note opens into, which until now did not exist: tapping a note on the list did nothing
/// at all, so the phone could list notes and never read one.
/// </summary>
public sealed class NoteDetailScreenTests
{
    [Fact]
    public async Task A_note_opens_showing_what_it_says()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");

        var screen = await context.OpenAsync(note.LocalId);

        Assert.Equal("Shopping", screen.Title);
        Assert.Equal(["milk", "bread"], screen.Lines.Select(line => line.Text));
    }

    [Fact]
    public async Task A_line_added_is_kept()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        screen.NewLine = "eggs";
        await screen.AddLineCommand.ExecuteAsync(null);

        Assert.Equal(["milk", "eggs"], screen.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// A line was a read-only label on this screen, so anything written into a note could never be
    /// corrected - the note had to be deleted and written again. Orbit.Web edits its lines in place.
    /// </summary>
    [Fact]
    public async Task A_line_can_be_corrected_after_it_is_written()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "mikl");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = "milk";
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(note.LocalId);
        Assert.Equal(["milk"], reopened.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// Leaving the screen is the other way an edit ends, and the one that used to lose it: every other
    /// action here saves the whole note, so only a line typed and then left alone was at risk.
    /// </summary>
    [Fact]
    public async Task A_line_edited_and_left_alone_is_still_written_down()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "mikl");
        var screen = await context.OpenAsync(note.LocalId);

        screen.Lines[0].Text = "milk";
        await screen.CloseAsync();

        var reopened = await context.OpenAsync(note.LocalId);
        Assert.Equal(["milk"], reopened.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// Two lines that say the same thing are still two lines. They were compared by value, so ticking
    /// one ticked both - and deleting one deleted both.
    /// </summary>
    [Fact]
    public async Task Two_lines_that_say_the_same_thing_are_told_apart()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        await screen.ToggleChecklistCommand.ExecuteAsync(screen.Lines[0]);
        await screen.ToggleCheckedCommand.ExecuteAsync(screen.Lines[0]);

        Assert.True(screen.Lines[0].IsChecked);
        Assert.False(screen.Lines[1].IsChecklistItem);
    }

    /// <summary>Orbit.Web's "Checklist item" button starts a tickable line rather than converting one.</summary>
    [Fact]
    public async Task A_checklist_item_can_be_started_directly()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping");
        var screen = await context.OpenAsync(note.LocalId);

        screen.NewLine = "eggs";
        await screen.AddChecklistItemCommand.ExecuteAsync(null);

        Assert.True(screen.Lines.Single().IsChecklistItem);
        Assert.False(screen.Lines.Single().IsChecked);
    }

    [Fact]
    public async Task A_line_can_become_a_checklist_item_and_be_ticked()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        await screen.ToggleChecklistCommand.ExecuteAsync(screen.Lines[0]);
        await screen.ToggleCheckedCommand.ExecuteAsync(screen.Lines[0]);

        Assert.True(screen.Lines[0].IsChecklistItem);
        Assert.True(screen.Lines[0].IsChecked);
    }

    /// <summary>
    /// Ticking is only for checklist items. Prose has nothing to tick, and letting it through would
    /// write a checked line that the editor then shows with no box beside it.
    /// </summary>
    [Fact]
    public async Task Prose_cannot_be_ticked()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        await screen.ToggleCheckedCommand.ExecuteAsync(screen.Lines[0]);

        Assert.False(screen.Lines[0].IsChecked);
    }

    [Fact]
    public async Task A_line_removed_is_gone()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk", "bread");
        var screen = await context.OpenAsync(note.LocalId);

        await screen.RemoveLineCommand.ExecuteAsync(screen.Lines[0]);

        Assert.Equal(["bread"], screen.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// A private note's words live inside an encrypted payload the phone has no key for - the server
    /// sends an empty title and no lines at all. Offering an editor over that would present an empty
    /// note and, on save, send the emptiness back.
    /// </summary>
    [Fact]
    public async Task A_private_note_opens_read_only_and_says_why()
    {
        using var context = new ScreenContext();
        var note = await context.AddPrivateNoteAsync();

        var screen = await context.OpenAsync(note.LocalId);

        Assert.True(screen.IsReadOnly);
        Assert.False(screen.CanEdit);
        Assert.NotEqual(string.Empty, screen.ReadOnlyReason);
    }

    [Fact]
    public async Task Deleting_a_note_goes_back_to_the_list()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping", "milk");
        var screen = await context.OpenAsync(note.LocalId);

        await screen.DeleteCommand.ExecuteAsync(null);

        Assert.Contains(nameof(IScreenNavigator.ShowNotes), context.Navigator.Destinations);
        Assert.Empty(await context.Notes.GetAllAsync());
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-27T10:00:00Z"));
        private readonly NoteSynchronizer _synchronizer;

        public ScreenContext()
        {
            Server = new FakeNotesServer(_clock);
            Notes = new LocalNoteRepository(_localStore, _clock, FixedNetworkStatus.Online);
            _synchronizer = new NoteSynchronizer(
                _localStore, new NotesClient(Server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<NoteSynchronizer>.Instance);
        }

        public FakeNotesServer Server { get; }

        public LocalNoteRepository Notes { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        public Task<LocalNote> AddNoteAsync(string title, params string[] lines)
            => Notes.CreateAsync(title, [.. lines.Select(line => new NoteContentLineDto(line, false, false))]);

        /// <summary>
        /// As one arrives from the server: private, with the title and lines stripped and only a sealed
        /// payload left - see Orbit.Core.Notes.Note.ReadableOrSealed.
        /// </summary>
        public async Task<LocalNote> AddPrivateNoteAsync()
        {
            var note = await Notes.CreateAsync(string.Empty, []);
            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.Notes.Single(candidate => candidate.LocalId == note.LocalId);
            stored.IsPrivate = true;
            stored.EncryptedCiphertext = "AAAA";
            stored.EncryptedNonce = "BBBB";
            await dbContext.SaveChangesAsync();
            return note;
        }

        public async Task<NoteDetailViewModel> OpenAsync(Guid localId)
        {
            var screen = new NoteDetailViewModel(
                Notes, _synchronizer, new NotesClient(Server.ToHttpClient()), NothingIsBeingEdited(_clock),
                new Translations(new InMemoryLanguageStore()),
                ShareTestPanel.For(_localStore, new ChatRepository(_localStore, _clock)), Navigator);

            screen.Open(localId);
            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        /// <summary>
        /// A lock over a fake server that answers every claim with "yours" - these tests are about the
        /// editor, and EditLockTests covers what happens when somebody else is in it.
        /// </summary>
        private static EditLock NothingIsBeingEdited(TimeProvider clock)
            => new(FixedNetworkStatus.Online, clock, new Translations(new InMemoryLanguageStore()));

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}

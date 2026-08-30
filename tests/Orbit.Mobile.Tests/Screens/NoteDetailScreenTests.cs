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
using Orbit.Mobile.Crypto;

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
    /// A private note nothing here can open - no key on this device. Offering an editor over that would
    /// present an empty note and, on save, send the emptiness back over the sealed copy.
    /// </summary>
    [Fact]
    public async Task A_private_note_this_device_cannot_open_stays_read_only_and_says_why()
    {
        using var context = new ScreenContext(PrivateContent.SignedInWithoutAKey(Owner));
        var note = await context.AddSealedNoteAsync();

        var screen = await context.OpenAsync(note.LocalId);

        Assert.True(screen.IsReadOnly);
        Assert.False(screen.CanEdit);
        Assert.NotEqual(string.Empty, screen.ReadOnlyReason);
    }

    /// <summary>
    /// The point of the whole exercise: the phone can now make a note private, and what it writes down
    /// is what the server is allowed to hold - nothing readable, and a sealed payload beside it.
    /// </summary>
    [Fact]
    public async Task Making_a_note_private_seals_its_words_and_leaves_the_readable_columns_empty()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var note = await context.AddNoteAsync("Bank details", "sort code");
        var screen = await context.OpenAsync(note.LocalId);

        screen.IsPrivate = true;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var stored = context.Stored(note.LocalId);
        Assert.True(stored.IsPrivate);
        Assert.Equal(string.Empty, stored.Title);
        Assert.Empty(stored.Content);
        Assert.NotNull(stored.EncryptedContent);
    }

    [Fact]
    public async Task A_note_this_device_sealed_opens_again_with_its_words_back()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var note = await context.AddNoteAsync("Bank details", "sort code");
        var screen = await context.OpenAsync(note.LocalId);
        screen.IsPrivate = true;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(note.LocalId);

        Assert.False(reopened.IsReadOnly);
        Assert.True(reopened.IsPrivate);
        Assert.Equal("Bank details", reopened.Title);
        Assert.Equal(["sort code"], reopened.Lines.Select(line => line.Text));
    }

    /// <summary>
    /// A private note is offered to nobody: the server holds no readable copy to hand over, which is
    /// what being private means. Orbit.Web hides the same panel for the same reason.
    /// </summary>
    [Fact]
    public async Task A_private_note_is_not_offered_to_anybody()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var note = await context.AddNoteAsync("Bank details", "sort code");
        var screen = await context.OpenAsync(note.LocalId);
        screen.IsPrivate = true;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(note.LocalId);

        Assert.False(reopened.CanBeShared);
    }

    [Fact]
    public async Task Turning_private_off_puts_the_words_back_where_the_server_can_read_them()
    {
        using var context = new ScreenContext(PrivateContent.HoldingAKeyFor(Owner));
        var note = await context.AddNoteAsync("Bank details", "sort code");
        var screen = await context.OpenAsync(note.LocalId);
        screen.IsPrivate = true;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        var reopened = await context.OpenAsync(note.LocalId);
        reopened.IsPrivate = false;
        await reopened.SaveLinesCommand.ExecuteAsync(null);

        var stored = context.Stored(note.LocalId);
        Assert.False(stored.IsPrivate);
        Assert.Equal("Bank details", stored.Title);
        Assert.Null(stored.EncryptedContent);
    }

    /// <summary>
    /// Sealing needs the account's own key, and the key gate is where a device without one gets it -
    /// the same place chat sends people. Saving the words in the clear instead would break the promise
    /// the switch had just made.
    /// </summary>
    [Fact]
    public async Task Making_a_note_private_without_a_key_asks_for_it_rather_than_saving()
    {
        using var context = new ScreenContext(PrivateContent.SignedInWithoutAKey(Owner));
        var note = await context.AddNoteAsync("Bank details", "sort code");
        var screen = await context.OpenAsync(note.LocalId);

        screen.IsPrivate = true;
        await screen.SaveLinesCommand.ExecuteAsync(null);

        Assert.Contains(nameof(IScreenNavigator.ShowChatKeyGate), context.Navigator.Destinations);
        Assert.False(context.Stored(note.LocalId).IsPrivate);
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

    /// <summary>Whoever is signed in - only its identity matters, as the key is kept per account.</summary>
    private static readonly Guid Owner = Guid.Parse("11111111-0000-4000-8000-000000000001");

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-27T10:00:00Z"));
        private readonly NoteSynchronizer _synchronizer;
        private readonly PrivateContentSealer _privateContent;

        public ScreenContext(PrivateContentSealer? privateContent = null)
        {
            _privateContent = privateContent ?? PrivateContent.WithoutAKey();
            Server = new FakeNotesServer(_clock);
            Notes = new LocalNoteRepository(_localStore, _clock, FixedNetworkStatus.Online, _privateContent);
            _synchronizer = new NoteSynchronizer(
                _localStore, new NotesClient(Server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<NoteSynchronizer>.Instance);
        }

        /// <summary>The row as it really sits in the database, rather than as a read hands it back opened.</summary>
        public LocalNote Stored(Guid localId)
        {
            using var dbContext = _localStore.CreateDbContext();
            return dbContext.Notes.Single(note => note.LocalId == localId);
        }

        public FakeNotesServer Server { get; }

        public LocalNoteRepository Notes { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        public Task<LocalNote> AddNoteAsync(string title, params string[] lines)
            => Notes.CreateAsync(title, [.. lines.Select(line => new NoteContentLineDto(line, false, false))]);

        /// <summary>
        /// As one arrives from the server: private, with the title and lines stripped and only a sealed
        /// payload left - see Orbit.Core.Notes.Note.ReadableOrSealed. The payload is nonsense on purpose:
        /// these tests are about a note this device cannot open, and one sealed under a replaced key pair
        /// is indistinguishable from it.
        /// </summary>
        public async Task<LocalNote> AddSealedNoteAsync()
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
                new Translations(new InMemoryLanguageStore()), _privateContent,
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

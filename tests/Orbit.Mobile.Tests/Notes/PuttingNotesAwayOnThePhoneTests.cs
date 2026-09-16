using Orbit.Contracts.Notes;
using Orbit.Core.Folders;
using Orbit.Mobile.Api;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Folders;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Security;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Orbit.Mobile.Tests.Notes;

/// <summary>
/// Putting a note away on the phone and bringing it back - the half that was missing while the browser
/// already had it, so something archived in a browser came down here and stayed on whichever tab it was
/// on. See Orbit.Core.Folders.BuiltInFolder.Archived.
/// </summary>
public sealed class PuttingNotesAwayOnThePhoneTests
{
    /// <summary>
    /// Archived beats even a folder its owner filed it into, which is the whole point: putting something
    /// away is a decision about whether it is in front of the reader at all - see FolderPlacement.
    /// </summary>
    [Fact]
    public async Task Putting_a_note_away_moves_it_out_of_the_folder_it_was_in()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Old receipts");
        var screen = await context.OpenAsync();
        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);
        await context.Notes.FileAsync(note.LocalId, work.LocalId);

        await context.Notes.ArchiveAsync(note.LocalId, isArchived: true);
        await screen.LoadCommand.ExecuteAsync(null);

        screen.ChooseFolderCommand.Execute(FolderKey.Of(work.LocalId));
        Assert.Empty(screen.Notes);
        // Said about the folder, not the account: "No notes." here read as the note having gone.
        Assert.Equal("Nothing in this folder.", screen.NothingHereMessage);

        screen.ChooseFolderCommand.Execute(FolderKey.Of(BuiltInFolder.Archived));
        Assert.Equal("Old receipts", Assert.Single(screen.Notes).DisplayTitle);
    }

    /// <summary>And an account with no notes at all is told that, rather than about a folder.</summary>
    [Fact]
    public async Task An_account_with_no_notes_is_told_it_has_none()
    {
        using var context = new ScreenContext();

        var screen = await context.OpenAsync();

        Assert.Equal("No notes.", screen.NothingHereMessage);
    }

    /// <summary>
    /// Its folder id is left alone while it is away, so bringing it back puts it under the folder it
    /// came from rather than somewhere a rule had to choose.
    /// </summary>
    [Fact]
    public async Task Bringing_one_back_puts_it_where_it_was()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Old receipts");
        var screen = await context.OpenAsync();
        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);
        await context.Notes.FileAsync(note.LocalId, work.LocalId);
        await context.Notes.ArchiveAsync(note.LocalId, isArchived: true);

        await context.Notes.ArchiveAsync(note.LocalId, isArchived: false);
        await screen.LoadCommand.ExecuteAsync(null);

        screen.ChooseFolderCommand.Execute(FolderKey.Of(work.LocalId));
        Assert.Equal("Old receipts", Assert.Single(screen.Notes).DisplayTitle);
    }

    /// <summary>
    /// Archiving travels on its own endpoint, like the filing beside it, so a save never carries it -
    /// see ArchiveRequest, which says what a save carrying it would undo.
    /// </summary>
    [Fact]
    public async Task Putting_one_away_reaches_the_server()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Old receipts");
        await context.SynchroniseAsync();

        await context.Notes.ArchiveAsync(note.LocalId, isArchived: true);
        await context.SynchroniseAsync();

        Assert.True(Assert.Single(context.OnTheServer).IsArchived);
    }

    /// <summary>
    /// One put away before the server ever heard of it: the create has no room for the flag, so the
    /// archiving goes out in the pass straight after that create succeeds rather than being lost.
    /// </summary>
    [Fact]
    public async Task One_put_away_before_it_was_ever_sent_still_arrives_put_away()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Old receipts");

        await context.Notes.ArchiveAsync(note.LocalId, isArchived: true);
        await context.SynchroniseAsync();

        Assert.True(Assert.Single(context.OnTheServer).IsArchived);
    }

    /// <summary>
    /// The other direction: something put away in a browser comes down and is away here too, which is
    /// exactly what did not happen while the phone had no column for it.
    /// </summary>
    [Fact]
    public async Task One_put_away_in_a_browser_comes_down_put_away()
    {
        using var context = new ScreenContext();
        context.PutAwayOnTheServer("Old receipts");

        await context.SynchroniseAsync();
        var screen = await context.OpenAsync();

        Assert.Empty(screen.Notes);
        screen.ChooseFolderCommand.Execute(FolderKey.Of(BuiltInFolder.Archived));
        Assert.Equal("Old receipts", Assert.Single(screen.Notes).DisplayTitle);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-09-15T10:00:00Z"));
        private readonly FakeNotesServer _server;
        private readonly NoteSynchronizer _synchronizer;

        public ScreenContext()
        {
            _server = new FakeNotesServer(_clock);
            Notes = new LocalNoteRepository(
                _localStore, _clock, FixedNetworkStatus.Online, PrivateContent.HoldingAKeyFor(Guid.NewGuid()));
            Folders = new LocalFolderRepository(_localStore, _clock);
            _synchronizer = new NoteSynchronizer(
                _localStore, new NotesClient(_server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<NoteSynchronizer>.Instance);
        }

        public LocalNoteRepository Notes { get; }

        public LocalFolderRepository Folders { get; }

        public IReadOnlyCollection<NoteDto> OnTheServer => _server.Notes;

        public Task<LocalNote> AddNoteAsync(string title)
            => Notes.CreateAsync(title, [new NoteContentLineDto("milk", false, false)]);

        /// <summary>A note the server already holds and already has away - as a browser would leave it.</summary>
        public void PutAwayOnTheServer(string title)
            => _server.ReplaceForTest(_server.AddNote(title) with { IsArchived = true });

        public Task<SyncResult> SynchroniseAsync() => _synchronizer.SynchroniseAsync();

        public async Task<NotesViewModel> OpenAsync()
        {
            var screen = new NotesViewModel(
                Notes, _synchronizer, new NotesClient(_server.ToHttpClient()), FixedNetworkStatus.Online,
                new Translations(new InMemoryLanguageStore()),
                new PrivateItemGate(new FixedDeviceAuthentication()),
                new SyncState(Reachability.Online, _clock), new RecordingScreenNavigator(), _clock,
                new InMemoryListArrangementStore(), Folders, new InMemoryChosenFolderStore(),
                TestDoubles.Folders.SynchronizerAgainstNobody(_localStore, _clock));

            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public void Dispose()
        {
            _server.Dispose();
            _localStore.Dispose();
        }
    }
}

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
/// Folders on the phone: the tabs the browser draws above its cards, as entries in the menu under the
/// screen's name. Until now the phone never sent a folder at all, so everything it made landed in a
/// built-in one and everything filed in the browser looked unfiled here.
/// </summary>
public sealed class NoteFoldersTests
{
    [Fact]
    public async Task A_screen_opens_on_the_ordinary_folder_and_shows_what_is_in_it()
    {
        using var context = new ScreenContext();
        await context.AddNoteAsync("Shopping");
        var screen = await context.OpenAsync();

        Assert.Equal(FolderKey.Of(BuiltInFolder.Public), screen.Folders.Chosen);
        Assert.Equal("Shopping", Assert.Single(screen.Notes).DisplayTitle);
    }

    /// <summary>
    /// A note filed into a folder leaves the built-in one it was in. Both are tabs on the same row, so
    /// a note is in exactly one of them - see FolderPlacement, which is the rule both clients read.
    /// </summary>
    [Fact]
    public async Task Filing_a_note_moves_it_out_of_the_folder_it_was_in()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping");
        var screen = await context.OpenAsync();
        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);

        await context.Notes.FileAsync(note.LocalId, work.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Empty(screen.Notes);

        screen.ChooseFolderCommand.Execute(FolderKey.Of(work.LocalId));
        Assert.Equal("Shopping", Assert.Single(screen.Notes).DisplayTitle);
    }

    /// <summary>
    /// Every folder is offered whether or not it holds anything, with the count beside it. A tab that
    /// appeared only once something was in it could never be filed into in the first place.
    /// </summary>
    [Fact]
    public async Task The_menu_counts_what_is_in_each_folder()
    {
        using var context = new ScreenContext();
        await context.AddNoteAsync("Shopping");
        await context.AddNoteAsync("Reading");
        var screen = await context.OpenAsync();
        await context.Folders.CreateAsync("Work", FolderScope.Notes);
        await screen.LoadCommand.ExecuteAsync(null);

        var choices = screen.FolderChoices.ToDictionary(choice => choice.Name, choice => choice.Count);

        Assert.Equal(2, choices["Public"]);
        Assert.Equal(0, choices["Private"]);
        Assert.Equal(0, choices["Work"]);
        // A note has nothing to finish, so the notes screen has no Finished tab at all.
        Assert.DoesNotContain("Finished", choices.Keys);
    }

    /// <summary>
    /// Making one moves the screen to it: making a folder is how somebody says where the next thing
    /// goes, and leaving them looking at the one they just left would be answering a question nobody
    /// asked.
    /// </summary>
    [Fact]
    public async Task Making_a_folder_opens_it()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();

        await screen.MakeFolderCommand.ExecuteAsync("Work");

        Assert.Equal("Work", screen.ChosenFolderName);
        Assert.Contains(screen.FolderChoices, choice => choice.Name == "Work" && choice.IsChosen);
    }

    /// <summary>
    /// Deleting a folder empties it rather than taking what is in it - which is what the server does
    /// too. A folder is a place to put things, and getting rid of the place is not a decision to get
    /// rid of them.
    /// </summary>
    [Fact]
    public async Task Deleting_a_folder_keeps_what_was_in_it()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping");
        var screen = await context.OpenAsync();
        await screen.MakeFolderCommand.ExecuteAsync("Work");
        await context.Notes.FileAsync(note.LocalId, screen.Folders.Chosen.FolderId!.Value);
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.DeleteFolderCommand.ExecuteAsync(null);

        // Back where anything unfiled already is, and the screen with it.
        Assert.Equal(FolderKey.Of(BuiltInFolder.Public), screen.Folders.Chosen);
        Assert.Equal("Shopping", Assert.Single(screen.Notes).DisplayTitle);
    }

    /// <summary>
    /// A sealed note is in Private wherever it was filed - unless its owner filed it somewhere of their
    /// own, because filing beats every built-in folder. See FolderPlacement, which says why.
    /// </summary>
    [Fact]
    public async Task A_private_note_is_in_the_private_folder_until_it_is_filed_somewhere()
    {
        using var context = new ScreenContext();
        var note = await context.AddPrivateNoteAsync("Bank papers");
        var screen = await context.OpenAsync();

        screen.ChooseFolderCommand.Execute(FolderKey.Of(BuiltInFolder.Private));
        Assert.Single(screen.Notes);

        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);
        await context.Notes.FileAsync(note.LocalId, work.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Empty(screen.Notes);
        screen.ChooseFolderCommand.Execute(FolderKey.Of(work.LocalId));
        Assert.Single(screen.Notes);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-09-10T10:00:00Z"));
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

        public Task<LocalNote> AddNoteAsync(string title)
            => Notes.CreateAsync(title, [new NoteContentLineDto("milk", false, false)]);

        public async Task<LocalNote> AddPrivateNoteAsync(string title)
        {
            var note = await Notes.CreateAsync(title, [new NoteContentLineDto("account", false, false)]);
            await Notes.UpdateAsync(
                note.LocalId,
                new NoteContent(title, [new NoteContentLineDto("account", false, false)], "Normal", IsPrivate: true));
            return note;
        }

        public async Task<NotesViewModel> OpenAsync()
        {
            var screen = new NotesViewModel(
                Notes, _synchronizer, new NotesClient(_server.ToHttpClient()), FixedNetworkStatus.Online,
                new Translations(new InMemoryLanguageStore()),
                new PrivateItemGate(new FixedDeviceAuthentication()),
                new SyncState(FixedNetworkStatus.Online, _clock), new RecordingScreenNavigator(), _clock,
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

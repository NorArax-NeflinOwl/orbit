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

        Assert.Equal(FolderKey.Of(BuiltInFolder.All), screen.Folders.Chosen);
        Assert.Equal("Shopping", Assert.Single(screen.Notes).DisplayTitle);
    }

    /// <summary>
    /// A note filed into a folder is under that folder and still under All, which is what All means since
    /// 2026-09-24 - see FolderKey.Holds, the rule both clients read. Filing it used to take it off the
    /// folder the screen opens on, so the only way to see everything was to have filed nothing.
    /// </summary>
    [Fact]
    public async Task A_filed_note_is_under_its_folder_and_still_under_All()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping");
        var screen = await context.OpenAsync();
        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);

        await context.Notes.FileAsync(note.LocalId, work.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Shopping", Assert.Single(screen.Notes).DisplayTitle);

        screen.ChooseFolderCommand.Execute(FolderKey.Of(work.LocalId));
        Assert.Equal("Shopping", Assert.Single(screen.Notes).DisplayTitle);
    }

    /// <summary>
    /// And a folder still narrows: what is filed somewhere else is not under it. The half of the rule
    /// above that All widening could have quietly undone.
    /// </summary>
    [Fact]
    public async Task A_folder_holds_only_what_was_filed_into_it()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping");
        await context.AddNoteAsync("Recipes");
        var screen = await context.OpenAsync();
        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);

        await context.Notes.FileAsync(note.LocalId, work.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);
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

        Assert.Equal(2, choices["All"]);
        Assert.Equal(0, choices["Private"]);
        Assert.Equal(0, choices["Work"]);
        // A note has nothing to finish, so the notes screen has no Finished tab at all.
        Assert.DoesNotContain("Finished", choices.Keys);
    }

    /// <summary>
    /// A folder holding something the reader has not seen says so, so somebody following a notification
    /// can tell which one holds what they were sent to - the screen opens on whatever folder it was last
    /// left on, and the tabs are entries in a menu here. The browser puts the same dot on its tab; asked
    /// for on 2026-09-18 for both clients and only built in the browser then.
    /// </summary>
    [Fact]
    public async Task A_folder_holding_something_unseen_says_so()
    {
        using var context = new ScreenContext();
        var shared = await context.AddNoteAsync("Shopping");
        // A note the server has never seen has no address to be pointed at, so it is pushed first.
        await context.RaiseNewsAboutAsync(await context.PushAndReadTheServerIdAsync(shared.LocalId));

        var screen = await context.OpenAsync();

        Assert.Equal(
            ["All"],
            screen.FolderChoices.Where(choice => choice.HasNews).Select(choice => choice.Name));
    }

    [Fact]
    public async Task And_a_folder_holding_nothing_unseen_says_nothing()
    {
        using var context = new ScreenContext();
        await context.AddNoteAsync("Shopping");
        var screen = await context.OpenAsync();

        Assert.DoesNotContain(screen.FolderChoices, choice => choice.HasNews);
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
    /// A folder is renamed where it was named: the same row, opened on its present name, with the
    /// button reading Rename. The server, the client and the store could all rename one and no screen
    /// asked - the app has no text prompt of its own and Android's would sit badly beside Orbit's panel.
    /// </summary>
    [Fact]
    public async Task A_folder_is_renamed_in_the_row_it_was_named_in()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        await screen.MakeFolderCommand.ExecuteAsync("Wrok");
        var folderId = screen.Folders.Chosen.FolderId!.Value;

        screen.StartRenamingTheOpenFolder();
        Assert.Equal("Wrok", screen.NewFolderName);
        Assert.Equal("Rename", screen.FolderRowAction);

        await screen.MakeFolderCommand.ExecuteAsync("Work");

        Assert.Equal("Work", (await context.Folders.GetAllAsync(FolderScope.Notes)).Single(folder => folder.LocalId == folderId).Name);
        // Still on it, under its new name, and the row back to naming new ones.
        Assert.Equal("Work", screen.ChosenFolderName);
        Assert.Null(screen.FolderBeingRenamed);
        Assert.Equal("Add", screen.FolderRowAction);
        Assert.Single(screen.FolderChoices, choice => choice.Name == "Work");
    }

    /// <summary>
    /// Taking a folder off the dashboard is done from the page it belongs to, and takes it off *there*
    /// and nowhere else: the notes page still has it, with everything in it. What the dashboard then
    /// does with it is DashboardScreenTests' half of the same rule.
    /// </summary>
    [Fact]
    public async Task Hiding_a_folder_on_the_dashboard_leaves_it_on_its_own_page()
    {
        using var context = new ScreenContext();
        var screen = await context.OpenAsync();
        await screen.MakeFolderCommand.ExecuteAsync("Receipts");
        var folderId = screen.Folders.Chosen.FolderId!.Value;

        Assert.False(screen.IsChosenFolderHiddenOnTheDashboard);
        screen.ToggleShownOnTheDashboardCommand.Execute(null);

        Assert.True(screen.IsChosenFolderHiddenOnTheDashboard);
        await screen.LoadCommand.ExecuteAsync(null);
        Assert.Contains(screen.FolderChoices, choice => choice.Name == "Receipts");
        Assert.Equal(FolderKey.Of(folderId), screen.Folders.Chosen);

        // And put back, because a folder hidden by accident has to be findable again from the same menu.
        screen.ToggleShownOnTheDashboardCommand.Execute(null);
        Assert.False(screen.IsChosenFolderHiddenOnTheDashboard);
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
        Assert.Equal(FolderKey.Of(BuiltInFolder.All), screen.Folders.Chosen);
        Assert.Equal("Shopping", Assert.Single(screen.Notes).DisplayTitle);
    }

    /// <summary>
    /// The bar says which folder the screen is being read under, because on the phone the folders are
    /// in a menu that is shut - see ScreenTitleWithFolder. Asked for on 2026-09-24.
    /// </summary>
    [Fact]
    public async Task The_screens_name_says_which_folder_is_open()
    {
        using var context = new ScreenContext();
        await context.AddNoteAsync("Shopping");
        var screen = await context.OpenAsync();
        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Notes", screen.ScreenTitle);

        await screen.ChooseFolderCommand.ExecuteAsync(FolderKey.Of(work.LocalId));

        Assert.Equal("Notes · Work", screen.ScreenTitle);
    }

    /// <summary>
    /// The menu asks whether the open folder still holds anything before it offers to delete it - the
    /// browser greys the same entry on the same answer (Orbit.Web's FolderTabs.StillHolds, 2026-09-20).
    /// Until then the phone deleted a full folder and put everything back under Public, which is a
    /// press that quietly rearranges a screen's worth of notes under a word that promised to remove one.
    /// </summary>
    [Fact]
    public async Task A_folder_with_a_note_in_it_says_it_still_holds_something()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Shopping");
        var screen = await context.OpenAsync();
        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);

        await context.Notes.FileAsync(note.LocalId, work.LocalId);
        await screen.LoadCommand.ExecuteAsync(null);
        await screen.ChooseFolderCommand.ExecuteAsync(FolderKey.Of(work.LocalId));

        Assert.True(screen.Folders.ChosenStillHolds);
    }

    [Fact]
    public async Task And_an_empty_one_says_it_holds_nothing()
    {
        using var context = new ScreenContext();
        await context.AddNoteAsync("Shopping");
        var screen = await context.OpenAsync();
        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);

        await screen.LoadCommand.ExecuteAsync(null);
        await screen.ChooseFolderCommand.ExecuteAsync(FolderKey.Of(work.LocalId));

        Assert.False(screen.Folders.ChosenStillHolds);
        // And a built-in folder, which cannot be deleted at all, answers for nothing.
        await screen.ChooseFolderCommand.ExecuteAsync(FolderKey.Of(BuiltInFolder.All));
        Assert.False(screen.Folders.ChosenStillHolds);
    }

    /// <summary>
    /// The trap the count on the tab falls into: a note put away is drawn under Archived wherever it
    /// was filed, so the folder it is in counts zero and still holds it. Deleting the folder on that
    /// count would move a note nobody was looking at.
    /// </summary>
    [Fact]
    public async Task A_folder_holding_nothing_but_a_note_put_away_still_holds_it()
    {
        using var context = new ScreenContext();
        var note = await context.AddNoteAsync("Old receipts");
        var screen = await context.OpenAsync();
        var work = await context.Folders.CreateAsync("Work", FolderScope.Notes);

        await context.Notes.FileAsync(note.LocalId, work.LocalId);
        await context.Notes.ArchiveAsync(note.LocalId, isArchived: true);
        await screen.LoadCommand.ExecuteAsync(null);
        await screen.ChooseFolderCommand.ExecuteAsync(FolderKey.Of(work.LocalId));

        Assert.Equal(0, screen.FolderChoices.Single(choice => choice.Name == "Work").Count);
        Assert.True(screen.Folders.ChosenStillHolds);
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
            Notifications = new LocalNotificationRepository(_localStore);
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

        /// <summary>
        /// The feed this phone holds, for the folder that says it holds something unseen - see
        /// FolderChoice.HasNews. Nothing in it unless a test raises something.
        /// </summary>
        public LocalNotificationRepository Notifications { get; }

        /// <summary>
        /// Says that something unread points at one note, as the server would have said it - the phone
        /// keeps the feed by the addresses the entries carry, which is what UnreadNews matches.
        /// </summary>
        public Task RaiseNewsAboutAsync(Guid noteServerId)
            => Notifications.RaiseAsync(
                "NoteShared", "Shared with you", "A note", $"/notes/{noteServerId}", _clock.GetUtcNow());

        /// <summary>
        /// Pushes what is queued and hands back the id the server gave this note. A note it has never
        /// seen has no address anything could point at, so nothing can be unread about it.
        /// </summary>
        public async Task<Guid> PushAndReadTheServerIdAsync(Guid localId)
        {
            await _synchronizer.SynchroniseAsync(CancellationToken.None);
            return (await Notes.FindAsync(localId))!.ServerId!.Value;
        }

        public async Task<NotesViewModel> OpenAsync()
        {
            var screen = new NotesViewModel(
                Notes, _synchronizer, new NotesClient(_server.ToHttpClient()), FixedNetworkStatus.Online,
                new Translations(new InMemoryLanguageStore()),
                new PrivateItemGate(new FixedDeviceAuthentication()),
                new SyncState(Reachability.Online, _clock), new RecordingScreenNavigator(), _clock,
                new InMemoryListArrangementStore(), Folders, new InMemoryChosenFolderStore(),
                TestDoubles.Folders.SynchronizerAgainstNobody(_localStore, _clock),
                notifications: Notifications);

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

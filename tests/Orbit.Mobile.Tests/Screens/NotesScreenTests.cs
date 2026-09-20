using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Security;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The notes list, in the one respect a copy taken offline changes it: two notes with the same title
/// now exist, and the list is where somebody has to be able to tell them apart. Where the deciding
/// happens is the avatar's menu - see NavigationBarTests.
/// </summary>
public sealed class NotesScreenTests
{
    [Fact]
    public async Task A_copy_says_so_in_the_list()
    {
        using var context = new ListContext();
        var original = await context.AddNoteAsync("Team shopping");
        await context.Notes.CopyForEditingAsync(original.LocalId);

        var screen = await context.OpenAsync();

        Assert.Contains(screen.Notes, row => row.IsCopy);
        Assert.Contains(screen.Notes, row => !row.IsCopy);
    }

    /// <summary>
    /// The card's own menu, which is the only way a note leaves the list without being opened first -
    /// see NotesPage, which is where the question in front of it is asked.
    /// </summary>
    [Fact]
    public async Task Deleting_a_note_from_its_card_takes_it_off_the_list()
    {
        using var context = new ListContext();
        await context.AddNoteAsync("Team shopping");
        var screen = await context.OpenAsync();

        await screen.DeleteCommand.ExecuteAsync(Assert.Single(screen.Notes));

        Assert.Empty(screen.Notes);
    }

    /// <summary>Nothing to delete is not a crash: the menu hands back whatever row it was opened on.</summary>
    [Fact]
    public async Task Deleting_nothing_does_nothing()
    {
        using var context = new ListContext();
        await context.AddNoteAsync("Team shopping");
        var screen = await context.OpenAsync();

        await screen.DeleteCommand.ExecuteAsync(null);

        Assert.Single(screen.Notes);
    }

    /// <summary>
    /// The plus makes a note and hands the reader to it. It used to unfold a name field at the top of
    /// the screen and wait for a name before making anything, which is three presses for one intention
    /// and asks for the title in a box above a list rather than in the note (asked for 2026-09-20).
    /// </summary>
    [Fact]
    public async Task The_plus_makes_a_note_and_opens_it()
    {
        using var context = new ListContext();
        var screen = await context.OpenAsync();

        await screen.AddNoteCommand.ExecuteAsync(null);

        var made = Assert.Single(screen.Notes);
        Assert.Equal(nameof(IScreenNavigator.ShowNote), context.Navigator.LastDestination);
        Assert.Equal(made.LocalId, context.Navigator.LastNoteId);
    }

    /// <summary>
    /// Made on this phone before anything is typed, which is what the field above was doing: the store
    /// is what makes a note exist offline, and the screen it opens is where it gets its name.
    /// </summary>
    [Fact]
    public async Task The_note_the_plus_makes_is_on_the_phone_already()
    {
        using var context = new ListContext();
        var screen = await context.OpenAsync();

        await screen.AddNoteCommand.ExecuteAsync(null);

        Assert.Single(await context.Notes.GetAllAsync());
    }

    private sealed class ListContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-30T10:00:00Z"));
        private readonly NoteSynchronizer _synchronizer;
        private readonly FakeNotesServer _server;

        public ListContext()
        {
            _server = new FakeNotesServer(_clock);
            Notes = new LocalNoteRepository(
                _localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            _synchronizer = new NoteSynchronizer(
                _localStore, new NotesClient(_server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<NoteSynchronizer>.Instance);
        }

        public LocalNoteRepository Notes { get; }

        /// <summary>Where the screen sent the reader, for the plus - which opens what it made.</summary>
        public RecordingScreenNavigator Navigator { get; } = new();

        public Task<LocalNote> AddNoteAsync(string title)
            => Notes.CreateAsync(title, [new NoteContentLineDto("milk", false, false)]);

        public async Task<NotesViewModel> OpenAsync()
        {
            var screen = new NotesViewModel(
                Notes, _synchronizer, new NotesClient(_server.ToHttpClient()), FixedNetworkStatus.Online,
                new Translations(new InMemoryLanguageStore()),
                new PrivateItemGate(new FixedDeviceAuthentication()),
                new SyncState(Reachability.Online, _clock), Navigator, _clock,
                new InMemoryListArrangementStore(),
                new LocalFolderRepository(_localStore, _clock), new InMemoryChosenFolderStore(),
                Folders.SynchronizerAgainstNobody(_localStore, _clock));

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

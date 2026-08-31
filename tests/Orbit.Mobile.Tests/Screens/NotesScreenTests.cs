using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
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

        public Task<LocalNote> AddNoteAsync(string title)
            => Notes.CreateAsync(title, [new NoteContentLineDto("milk", false, false)]);

        public async Task<NotesViewModel> OpenAsync()
        {
            var screen = new NotesViewModel(
                Notes, _synchronizer, new NotesClient(_server.ToHttpClient()), FixedNetworkStatus.Online,
                new Translations(new InMemoryLanguageStore()),
                new PrivateItemGate(new FixedDeviceAuthentication()),
                new SyncState(FixedNetworkStatus.Online, _clock), new RecordingScreenNavigator());

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

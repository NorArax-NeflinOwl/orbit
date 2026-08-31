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
/// now exist, and the list is where somebody has to be able to tell them apart and find their way to
/// the window that decides between them.
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
    /// The only way to the review window. Hidden until there is something waiting, because a permanent
    /// link to an empty screen is one more thing on a list that is meant to be notes.
    /// </summary>
    [Fact]
    public async Task The_way_to_the_review_window_appears_only_when_something_is_waiting()
    {
        using var context = new ListContext();
        var original = await context.AddNoteAsync("Team shopping");

        Assert.False((await context.OpenAsync()).HasCopiesAwaitingReview);

        await context.Notes.CopyForEditingAsync(original.LocalId);

        Assert.True((await context.OpenAsync()).HasCopiesAwaitingReview);
    }

    /// <summary>A copy that has been decided on is history, and stops being a question.</summary>
    [Fact]
    public async Task A_kept_copy_moves_from_the_review_count_to_history()
    {
        using var context = new ListContext();
        var original = await context.AddNoteAsync("Team shopping");
        var copy = await context.Notes.CopyForEditingAsync(original.LocalId);
        await context.Notes.KeepCopyAsync(copy!.LocalId);

        var screen = await context.OpenAsync();

        Assert.False(screen.HasCopiesAwaitingReview);
        Assert.True(screen.HasHistory);
    }

    [Fact]
    public async Task Nothing_kept_leaves_history_out_of_the_way()
    {
        using var context = new ListContext();
        await context.AddNoteAsync("Team shopping");

        Assert.False((await context.OpenAsync()).HasHistory);
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

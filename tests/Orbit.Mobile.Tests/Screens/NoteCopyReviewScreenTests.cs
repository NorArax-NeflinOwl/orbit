using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Api;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// What happens when somebody comes back with a copy in their pocket. The three answers the window
/// offers are the whole of the offline conflict story on this phone, so each is checked for what it
/// leaves behind rather than only for what it says.
/// </summary>
public sealed class NoteCopyReviewScreenTests
{
    [Fact]
    public async Task A_copy_is_shown_beside_what_was_written_into_it()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        var copy = await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");

        var screen = await context.OpenAsync();

        var review = Assert.Single(screen.Reviews);
        Assert.Equal(copy.LocalId, review.LocalId);
        Assert.Equal([("milk", LineChange.Unchanged), ("bread", LineChange.Added)], Described(review.MyChanges));
        Assert.False(review.HasConflict);
    }

    /// <summary>
    /// Both sides moved. Nothing is broken by it - it is just the case where keeping one throws the
    /// other away, and the reader is entitled to be told that before they tap.
    /// </summary>
    [Fact]
    public async Task Both_sides_having_changed_is_shown_as_a_conflict()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");
        await context.ChangeTheOriginalAsync(original.LocalId, "milk", "eggs");

        var screen = await context.OpenAsync();

        var review = Assert.Single(screen.Reviews);
        Assert.True(review.HasConflict);
        Assert.Equal([("milk", LineChange.Unchanged), ("eggs", LineChange.Added)], Described(review.TheirChanges));
    }

    [Fact]
    public async Task Keeping_mine_writes_the_copys_words_onto_the_note_and_drops_the_copy()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepMineCommand.ExecuteAsync(screen.Reviews[0]);

        var stored = await context.Notes.FindAsync(original.LocalId);
        Assert.Equal(["milk", "bread"], stored!.Content.Select(line => line.Text));
        Assert.Empty(await context.Notes.GetCopiesOfAsync(original.LocalId));
        Assert.Empty(screen.Reviews);
    }

    /// <summary>
    /// Applying a copy is an edit like any other, so it has to leave the queue as an edit would - a
    /// review that changed only this phone would be undone by the next pull.
    /// </summary>
    [Fact]
    public async Task Keeping_mine_reaches_the_server()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.SynchroniseAsync();
        await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepMineCommand.ExecuteAsync(screen.Reviews[0]);

        var onTheServer = Assert.Single(context.Server.Notes, note => note.Title == "Team shopping");
        Assert.Equal(["milk", "bread"], onTheServer.Content.Select(line => line.Text));
    }

    [Fact]
    public async Task Keeping_theirs_leaves_the_note_alone_and_takes_the_copy_away()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepTheirsCommand.ExecuteAsync(screen.Reviews[0]);

        var stored = await context.Notes.FindAsync(original.LocalId);
        Assert.Equal(["milk"], stored!.Content.Select(line => line.Text));
        Assert.Empty(await context.Notes.GetCopiesOfAsync(original.LocalId));
    }

    /// <summary>
    /// A copy is a question, not a note, until a review answers it - so it goes nowhere. Pushed on
    /// sight, two of the three answers would have to take a note off the server again, and the reader
    /// would have watched a duplicate appear and disappear for no reason.
    /// </summary>
    [Fact]
    public async Task A_copy_awaiting_review_is_never_pushed()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        var copy = await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");

        await context.SynchroniseAsync();

        Assert.Empty(context.QueuedFor(copy.LocalId));
        Assert.Single(context.Server.Notes);
    }

    /// <summary>
    /// And keeping it is what makes it a note - so that is the point at which it goes. Left local, the
    /// version somebody chose to keep would exist on one phone only.
    /// </summary>
    [Fact]
    public async Task Keeping_both_puts_the_copy_on_the_server_as_a_note_of_its_own()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.SynchroniseAsync();
        await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepBothCommand.ExecuteAsync(screen.Reviews[0]);

        Assert.Equal(2, context.Server.Notes.Count);
        Assert.Contains(context.Server.Notes, note => note.Content.Any(line => line.Text == "bread"));
    }

    [Fact]
    public async Task Keeping_theirs_leaves_nothing_queued_behind()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        var copy = await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepTheirsCommand.ExecuteAsync(screen.Reviews[0]);

        Assert.Empty(context.QueuedFor(copy.LocalId));
        Assert.DoesNotContain(context.Server.Notes, note => note.Content.Any(line => line.Text == "bread"));
    }

    [Fact]
    public async Task Keeping_both_leaves_two_notes_and_stops_asking()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        var copy = await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepBothCommand.ExecuteAsync(screen.Reviews[0]);

        Assert.Empty(screen.Reviews);
        Assert.Equal(2, (await context.Notes.GetAllAsync()).Count);
        Assert.Equal(copy.LocalId, Assert.Single(await context.Notes.GetKeptCopiesAsync()).LocalId);
    }

    /// <summary>
    /// Deleted while the phone was away. There is nothing left to apply the copy over, so the window
    /// says so rather than offering three answers of which two do nothing.
    /// </summary>
    [Fact]
    public async Task A_copy_of_a_note_that_is_gone_says_so()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");
        await context.Notes.DeleteAsync(original.LocalId);

        var screen = await context.OpenAsync();

        Assert.True(Assert.Single(screen.Reviews).IsOriginalGone);
    }

    /// <summary>
    /// The note is shared and there is still no connection, so the policy refuses the write - and the
    /// copy is left where it is rather than quietly lost. The reader is told to come back to it.
    /// </summary>
    [Fact]
    public async Task Keeping_mine_while_still_offline_is_refused_and_keeps_the_copy()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();
        context.Network.Becomes(false);

        await screen.KeepMineCommand.ExecuteAsync(screen.Reviews[0]);

        Assert.True(screen.HasMessage);
        Assert.Single(screen.Reviews);
        Assert.Single(await context.Notes.GetCopiesOfAsync(original.LocalId));
    }

    [Fact]
    public async Task Nothing_taken_offline_means_nothing_to_review()
    {
        using var context = new ReviewContext();
        await context.AddSharedNoteAsync("Team shopping", "milk");

        var screen = await context.OpenAsync();

        Assert.True(screen.HasNothingToReview);
    }

    /// <summary>
    /// A copy kept on purpose is not still a question. It has been answered, and re-asking would make
    /// "keep both" mean "ask me again forever".
    /// </summary>
    [Fact]
    public async Task A_kept_copy_is_not_asked_about_again()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyAndWriteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();
        await screen.KeepBothCommand.ExecuteAsync(screen.Reviews[0]);

        Assert.True((await context.OpenAsync()).HasNothingToReview);
    }

    private static IReadOnlyList<(string, LineChange)> Described(IReadOnlyList<DiffLine> diff)
        => [.. diff.Select(line => (line.Text, line.Change))];

    private sealed class ReviewContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-30T10:00:00Z"));
        private readonly NoteSynchronizer _synchronizer;

        public ReviewContext()
        {
            Server = new FakeNotesServer(_clock);
            Notes = new LocalNoteRepository(_localStore, _clock, Network, PrivateContent.WithoutAKey());
            _synchronizer = new NoteSynchronizer(
                _localStore, new NotesClient(Server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<NoteSynchronizer>.Instance);
        }

        public FakeNotesServer Server { get; }

        public LocalNoteRepository Notes { get; }

        public FixedNetworkStatus Network { get; } = FixedNetworkStatus.Online;

        public RecordingScreenNavigator Navigator { get; } = new();

        public Task SynchroniseAsync() => _synchronizer.SynchroniseAsync(CancellationToken.None);

        public async Task<LocalNote> AddSharedNoteAsync(string title, params string[] lines)
        {
            var note = await Notes.CreateAsync(title, [.. lines.Select(Line)]);
            await using var dbContext = _localStore.CreateDbContext();
            dbContext.Notes.Single(candidate => candidate.LocalId == note.LocalId).IsShared = true;
            await dbContext.SaveChangesAsync();
            return note;
        }

        /// <summary>Takes a copy and writes the given lines into it, as somebody on a train would.</summary>
        public async Task<LocalNote> CopyAndWriteAsync(Guid originalLocalId, params string[] lines)
        {
            var copy = await Notes.CopyForEditingAsync(originalLocalId)
                ?? throw new InvalidOperationException("The copy was refused.");

            await Notes.UpdateAsync(
                copy.LocalId, new NoteContent(copy.Title, [.. lines.Select(Line)], copy.Priority));

            return copy;
        }

        /// <summary>Somebody else's change to the same note, as a pull would have brought it in.</summary>
        public async Task ChangeTheOriginalAsync(Guid localId, params string[] lines)
        {
            await using var dbContext = _localStore.CreateDbContext();
            var note = dbContext.Notes.Single(candidate => candidate.LocalId == localId);
            note.Content = [.. lines.Select(Line)];
            await dbContext.SaveChangesAsync();
        }

        /// <summary>What is still queued about one note - see the discarded-copy test.</summary>
        public IReadOnlyList<string> QueuedFor(Guid localId)
        {
            using var dbContext = _localStore.CreateDbContext();
            return [.. dbContext.Outbox.Where(entry => entry.LocalId == localId).Select(entry => entry.Operation.ToString())];
        }

        public async Task<NoteCopyReviewViewModel> OpenAsync()
        {
            var screen = new NoteCopyReviewViewModel(
                Notes, _synchronizer, new Translations(new InMemoryLanguageStore()), Navigator);

            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        private static NoteContentLineDto Line(string text) => new(text, false, false);

        public void Dispose()
        {
            Server.Dispose();
            _localStore.Dispose();
        }
    }
}

using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Copies;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens.Copies;

/// <summary>
/// One thing's history: the copies taken from it, and where each came from. Its whole job is the
/// reference - this note came from that one - which nothing else in the app records and which the notes
/// list cannot show without becoming a list of pairs.
///
/// Per thing rather than global, so these open it on a note and expect that note's story.
/// </summary>
public sealed class CopyHistoryScreenTests
{
    [Fact]
    public async Task A_kept_copy_is_listed_with_what_it_came_from()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");
        await context.KeepACopyOfAsync(original.LocalId);

        var screen = await context.OpenAsync(original.LocalId);

        var row = Assert.Single(screen.Rows);
        Assert.Equal("Team shopping", row.Title);
        Assert.Contains("Team shopping", row.Description);
        Assert.Equal(original.LocalId, row.OriginalLocalId);
    }

    /// <summary>
    /// A copy nobody has answered for yet is part of what happened to this note just as much as one
    /// that was kept, so it is listed - and marked as the open question it still is.
    /// </summary>
    [Fact]
    public async Task A_copy_still_awaiting_review_is_listed_and_says_so()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");
        await context.Notes.CopyForEditingAsync(original.LocalId);

        var row = Assert.Single((await context.OpenAsync(original.LocalId)).Rows);

        Assert.True(row.IsAwaitingReview);
    }

    /// <summary>
    /// Opened on the copy rather than on what it came from, the story is the same one - otherwise
    /// somebody standing on one of two versions would be told it has no history at all.
    /// </summary>
    [Fact]
    public async Task The_same_history_opens_from_either_version()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");
        var copy = await context.KeepACopyOfAsync(original.LocalId);

        Assert.Single((await context.OpenAsync(copy.LocalId)).Rows);
    }

    [Fact]
    public async Task Nothing_copied_leaves_the_window_empty_rather_than_wrong()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");

        Assert.True((await context.OpenAsync(original.LocalId)).HasNothing);
    }

    /// <summary>
    /// The copy outlives what it came from. Offering a way to a note that is gone would be a dead end,
    /// so the row says where it came from in words and stops offering to go there.
    /// </summary>
    [Fact]
    public async Task A_copy_whose_original_is_gone_keeps_its_place_without_a_dead_link()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");
        await context.KeepACopyOfAsync(original.LocalId);
        await context.Notes.DeleteAsync(original.LocalId);

        var row = Assert.Single((await context.OpenAsync(original.LocalId)).Rows);

        Assert.False(row.HasOriginal);
        Assert.Null(row.OriginalLocalId);
    }

    [Fact]
    public async Task Tapping_a_row_opens_the_copy()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");
        var copy = await context.KeepACopyOfAsync(original.LocalId);
        var screen = await context.OpenAsync(original.LocalId);

        screen.OpenCommand.Execute(screen.Rows[0]);

        Assert.Equal(copy.LocalId, context.Navigator.LastNoteId);
    }

    [Fact]
    public async Task The_reference_leads_to_the_note_it_came_from()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");
        await context.KeepACopyOfAsync(original.LocalId);
        var screen = await context.OpenAsync(original.LocalId);

        screen.OpenOriginalCommand.Execute(screen.Rows[0]);

        Assert.Equal(original.LocalId, context.Navigator.LastNoteId);
    }

    private sealed class HistoryContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-30T10:00:00Z"));

        public HistoryContext()
            => Notes = new LocalNoteRepository(
                _localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());

        public LocalNoteRepository Notes { get; }

        public RecordingScreenNavigator Navigator { get; } = new();

        public Task<LocalNote> AddNoteAsync(string title)
            => Notes.CreateAsync(title, [new NoteContentLineDto("milk", false, false)]);

        /// <summary>A copy taken and then kept, which is what a review's "keep both" leaves behind.</summary>
        public async Task<LocalNote> KeepACopyOfAsync(Guid originalLocalId)
        {
            var copy = await Notes.CopyForEditingAsync(originalLocalId)
                ?? throw new InvalidOperationException("The copy was refused.");

            await Notes.KeepCopyAsync(copy.LocalId);
            return copy;
        }

        public async Task<CopyHistoryViewModel> OpenAsync(Guid localId)
        {
            var screen = new CopyHistoryViewModel(
                [Notes], new Translations(new InMemoryLanguageStore()), Navigator);

            screen.Open(CopyKind.Note, localId);
            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public void Dispose() => _localStore.Dispose();
    }
}

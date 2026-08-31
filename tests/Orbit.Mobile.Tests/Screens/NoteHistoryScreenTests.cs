using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Notes;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Notes;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Where a copy kept by a review can be found again. Its whole job is the reference: this note came
/// from that one - which nothing else in the app records, and which the notes list cannot show without
/// becoming a list of pairs.
/// </summary>
public sealed class NoteHistoryScreenTests
{
    [Fact]
    public async Task A_kept_copy_is_listed_with_what_it_came_from()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");
        await context.KeepACopyOfAsync(original.LocalId);

        var screen = await context.OpenAsync();

        var row = Assert.Single(screen.Rows);
        Assert.Equal("Team shopping", row.Title);
        Assert.Contains("Team shopping", row.Description);
        Assert.Equal(original.LocalId, row.OriginalLocalId);
    }

    /// <summary>
    /// A copy that has not been through a review is not history - it is a question still waiting to be
    /// answered, and it belongs to the review window.
    /// </summary>
    [Fact]
    public async Task A_copy_still_awaiting_review_is_not_history_yet()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");
        await context.Notes.CopyForEditingAsync(original.LocalId);

        var screen = await context.OpenAsync();

        Assert.True(screen.HasNothing);
    }

    [Fact]
    public async Task Nothing_kept_leaves_the_window_empty_rather_than_wrong()
    {
        using var context = new HistoryContext();
        await context.AddNoteAsync("Team shopping");

        Assert.True((await context.OpenAsync()).HasNothing);
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

        var row = Assert.Single((await context.OpenAsync()).Rows);

        Assert.False(row.HasOriginal);
        Assert.Null(row.OriginalLocalId);
    }

    [Fact]
    public async Task Tapping_a_row_opens_the_copy()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");
        var copy = await context.KeepACopyOfAsync(original.LocalId);
        var screen = await context.OpenAsync();

        screen.OpenCommand.Execute(screen.Rows[0]);

        Assert.Equal(copy.LocalId, context.Navigator.LastNoteId);
    }

    [Fact]
    public async Task The_reference_leads_to_the_note_it_came_from()
    {
        using var context = new HistoryContext();
        var original = await context.AddNoteAsync("Team shopping");
        await context.KeepACopyOfAsync(original.LocalId);
        var screen = await context.OpenAsync();

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

        public async Task<NoteHistoryViewModel> OpenAsync()
        {
            var screen = new NoteHistoryViewModel(
                Notes, new Translations(new InMemoryLanguageStore()), Navigator);

            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        public void Dispose() => _localStore.Dispose();
    }
}

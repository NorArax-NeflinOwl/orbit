using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// The page of task lists. What it arranges is covered by TaskListViewTests; this is about the screen
/// remembering the answer, which it did not - the order went back to "most important first" every time
/// the page was opened, so a reader who wanted them alphabetically chose it again, and again.
/// </summary>
public sealed class TasksScreenTests : IDisposable
{
    private readonly LocalStore _localStore = new();
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-29T10:00:00Z"));
    private readonly FakeTasksServer _server;
    private readonly LocalTaskListRepository _taskLists;

    public TasksScreenTests()
    {
        _server = new FakeTasksServer(_clock);
        _taskLists = new LocalTaskListRepository(_localStore, _clock, FixedNetworkStatus.Online);
    }

    public void Dispose()
    {
        _server.Dispose();
        _localStore.Dispose();
    }

    /// <summary>Whatever was chosen last time, which on a phone opened afresh is the whole memory of it.</summary>
    private InMemoryTaskListSortOrderStore SortOrders { get; } = new();

    [Fact]
    public async Task The_order_opens_on_what_was_chosen_last_time()
    {
        SortOrders.Write(TaskListSortOrder.Alphabetical);

        var screen = await OpenAsync();

        Assert.Equal(TaskListSortOrder.Alphabetical, screen.SortOrder);
    }

    [Fact]
    public async Task Choosing_an_order_is_written_down_at_once()
    {
        var screen = await OpenAsync();

        screen.NextSortOrderCommand.Execute(null);

        Assert.Equal(screen.SortOrder, SortOrders.Remembered);
    }

    /// <summary>
    /// Written when it is chosen rather than when the screen leaves: there is no moment a screen is told
    /// it is going for good, and an order that took a restart to stick would read as one that had not.
    /// </summary>
    [Fact]
    public async Task The_order_survives_the_screen_being_opened_again()
    {
        var first = await OpenAsync();
        first.NextSortOrderCommand.Execute(null);
        var chosen = first.SortOrder;

        var second = await OpenAsync();

        Assert.Equal(chosen, second.SortOrder);
    }

    /// <summary>
    /// Nothing remembered is the order Orbit has always opened on, not a blank one - a first launch must
    /// not look different from every launch after it.
    /// </summary>
    [Fact]
    public async Task A_phone_that_has_never_chosen_opens_on_what_matters_most()
    {
        var screen = await OpenAsync();

        Assert.Equal(TaskListSortOrder.Priority, screen.SortOrder);
    }

    /// <summary>
    /// Filtering is a narrowing somebody does for a moment - "show me the overdue ones" - and bringing
    /// it back a week later would answer a question nobody asked twice. The web draws the line here too.
    /// </summary>
    [Fact]
    public async Task What_is_filtered_to_is_not_remembered()
    {
        var first = await OpenAsync();
        first.FilterByCommand.Execute(first.Filters.First(filter => filter.Status is not null));
        Assert.NotNull(first.StatusFilter);

        var second = await OpenAsync();

        Assert.Null(second.StatusFilter);
    }

    private async Task<TasksViewModel> OpenAsync()
    {
        var screen = new TasksViewModel(
            _taskLists,
            new TaskListSynchronizer(
                _localStore, new TasksClient(_server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<TaskListSynchronizer>.Instance),
            new TasksClient(_server.ToHttpClient()), FixedNetworkStatus.Online, SortOrders,
            new SyncState(FixedNetworkStatus.Online, _clock), new RecordingScreenNavigator(),
            new Translations(new InMemoryLanguageStore()));

        await screen.LoadCommand.ExecuteAsync(null);
        return screen;
    }
}

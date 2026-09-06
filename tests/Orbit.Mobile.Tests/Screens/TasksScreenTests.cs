using Orbit.Contracts.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Mobile.Api;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Security;
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
        _taskLists = new LocalTaskListRepository(_localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
    }

    public void Dispose()
    {
        _server.Dispose();
        _localStore.Dispose();
    }

    /// <summary>Whatever was chosen last time, which on a phone opened afresh is the whole memory of it.</summary>
    private InMemoryTaskListArrangementStore Arrangement { get; } = new();

    [Fact]
    public async Task The_order_opens_on_what_was_chosen_last_time()
    {
        Arrangement.WriteSortOrder(TaskListSortOrder.Alphabetical);

        var screen = await OpenAsync();

        Assert.Equal(TaskListSortOrder.Alphabetical, screen.SortOrder);
    }

    [Fact]
    public async Task Choosing_an_order_is_written_down_at_once()
    {
        var screen = await OpenAsync();

        screen.ChooseSortOrderCommand.Execute(Choice(screen, TaskListSortOrder.Oldest));

        Assert.Equal(TaskListSortOrder.Oldest, screen.SortOrder);
        Assert.Equal(screen.SortOrder, Arrangement.RememberedSortOrder);
    }

    /// <summary>
    /// Written when it is chosen rather than when the screen leaves: there is no moment a screen is told
    /// it is going for good, and an order that took a restart to stick would read as one that had not.
    /// </summary>
    [Fact]
    public async Task The_order_survives_the_screen_being_opened_again()
    {
        var first = await OpenAsync();
        first.ChooseSortOrderCommand.Execute(Choice(first, TaskListSortOrder.LeastImportantFirst));
        var chosen = first.SortOrder;

        var second = await OpenAsync();

        Assert.Equal(chosen, second.SortOrder);
    }

    /// <summary>
    /// The menu marks what is in force, because the button that says so is behind the menu once it is
    /// open - a list of six with no answer among them leaves the reader guessing.
    /// </summary>
    [Fact]
    public async Task The_menu_says_which_order_is_in_force()
    {
        Arrangement.WriteSortOrder(TaskListSortOrder.Oldest);

        var screen = await OpenAsync();

        Assert.Equal(TaskListSortOrder.Oldest, screen.SortChoices.Single(choice => choice.IsChosen).Order);
        Assert.Equal(Enum.GetValues<TaskListSortOrder>().Length, screen.SortChoices.Count);
    }

    /// <summary>
    /// Each under its own name. Describe ends in a catch-all, so an order added without a case of its
    /// own would quietly come out as "Z to A" - two entries reading alike is what that looks like.
    /// </summary>
    [Fact]
    public async Task No_two_orders_are_offered_under_the_same_name()
    {
        var screen = await OpenAsync();

        Assert.Equal(
            screen.SortChoices.Count, screen.SortChoices.Select(choice => choice.Name).Distinct().Count());
    }

    private static TaskListSortChoice Choice(TasksViewModel screen, TaskListSortOrder order)
        => screen.SortChoices.Single(choice => choice.Order == order);

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


    /// <summary>
    /// The order the reader puts the cards in is theirs, and it outlives the screen. Orbit.Web drags
    /// them into place; a phone moves them one at a time, which is a target a thumb can hit.
    /// </summary>
    [Fact]
    public async Task The_order_the_reader_moves_the_cards_into_is_kept()
    {
        await AddAsync("Move house", "Shopping", "Taxes");
        var screen = await OpenAsync();
        screen.ChooseSortOrderCommand.Execute(Choice(screen, TaskListSortOrder.Manual));

        screen.MoveListUpCommand.Execute(screen.TaskLists.Last());

        Assert.Equal(["Move house", "Taxes", "Shopping"], Titles(screen));
        Assert.Equal(Titles(screen), Titles(await OpenAsync()));
    }

    /// <summary>The ends are where the screen stops, not a failure - the first card has nowhere above it.</summary>
    [Fact]
    public async Task Moving_the_first_card_up_leaves_it_where_it_is()
    {
        await AddAsync("Move house", "Shopping");
        var screen = await OpenAsync();
        screen.ChooseSortOrderCommand.Execute(Choice(screen, TaskListSortOrder.Manual));

        screen.MoveListUpCommand.Execute(screen.TaskLists.First());

        Assert.Equal(["Move house", "Shopping"], Titles(screen));
    }

    /// <summary>
    /// A list made or shared since the reader last moved one is not in the wrong place - it is simply
    /// not placed yet, and it goes after the ones that are rather than pushing their order about.
    /// </summary>
    [Fact]
    public async Task A_card_nobody_has_placed_comes_after_the_ones_they_have()
    {
        await AddAsync("Move house", "Shopping");
        var screen = await OpenAsync();
        screen.ChooseSortOrderCommand.Execute(Choice(screen, TaskListSortOrder.Manual));
        screen.MoveListUpCommand.Execute(screen.TaskLists.Last());

        await AddAsync("Taxes");
        var reopened = await OpenAsync();

        Assert.Equal(["Shopping", "Move house", "Taxes"], Titles(reopened));
    }

    /// <summary>
    /// Moving one card while a filter is on must not disturb the cards the filter is hiding. They are
    /// still arranged - they are just not on screen - and writing back only what can be seen would drop
    /// every one of them to the end. Orbit.Web writes back what it can see, and loses them that way.
    /// </summary>
    [Fact]
    public async Task Moving_a_card_under_a_filter_leaves_the_hidden_ones_where_they_were()
    {
        await AddAsync("Move house", "Shopping", "Taxes");
        var screen = await OpenAsync();

        // Completed on the server and pulled down, not written straight into the local store: what the
        // phone holds for a status is whatever the last pull said, so a local one would not survive.
        CompleteOnTheServer("Shopping");
        await screen.LoadCommand.ExecuteAsync(null);
        screen.ChooseSortOrderCommand.Execute(Choice(screen, TaskListSortOrder.Manual));
        Assert.Equal(["Shopping", "Move house", "Taxes"], Titles(screen));

        screen.FilterByCommand.Execute(screen.Filters.Single(filter => filter.Status == "New"));
        screen.MoveListUpCommand.Execute(screen.TaskLists.Last());
        screen.FilterByCommand.Execute(screen.Filters.Single(filter => filter.Status is null));

        // Shopping is still first, where it was. Written back visible-only it would be last, unplaced.
        Assert.Equal(["Shopping", "Taxes", "Move house"], Titles(screen));
    }


    /// <summary>
    /// Folded down to its heading, and still there - the distinction Orbit.Web draws between folding a
    /// card and filtering it away. A list nobody is working on this week is still one they want to see.
    /// </summary>
    [Fact]
    public async Task A_folded_card_keeps_its_place_on_the_screen()
    {
        await AddAsync("Move house", "Shopping");
        var screen = await OpenAsync();

        screen.ToggleCollapsedCommand.Execute(screen.TaskLists.Single(row => row.Title == "Shopping"));

        Assert.Equal(2, screen.TaskLists.Count);
        Assert.True(screen.TaskLists.Single(row => row.Title == "Shopping").IsCollapsed);
        Assert.False(screen.TaskLists.Single(row => row.Title == "Move house").IsCollapsed);
    }
    /// <summary>
    /// The fold button is a glyph pointing up or down, which says nothing to a screen reader - so the
    /// row carries the same thing in words, and it names what tapping would do rather than the state
    /// the card is in. Found by measuring: the app had no accessible name on any control at all.
    /// </summary>
    [Fact]
    public async Task A_folded_card_says_in_words_what_its_glyph_would_do()
    {
        await AddAsync("Shopping");
        var screen = await OpenAsync();

        Assert.Equal("Collapse", screen.TaskLists.Single().FoldDescription);

        screen.ToggleCollapsedCommand.Execute(screen.TaskLists.Single());

        Assert.Equal("Expand", screen.TaskLists.Single().FoldDescription);
    }


    /// <summary>
    /// Folding is a standing answer about a card, not a narrowing somebody does for a moment, so unlike
    /// the filter chips it comes back with the screen - which is the line Orbit.Web draws as well.
    /// </summary>
    [Fact]
    public async Task Folding_survives_the_screen_being_opened_again()
    {
        await AddAsync("Move house");
        var first = await OpenAsync();
        first.ToggleCollapsedCommand.Execute(first.TaskLists.Single());

        var second = await OpenAsync();

        Assert.True(second.TaskLists.Single().IsCollapsed);
    }

    [Fact]
    public async Task Folding_a_card_twice_opens_it_again()
    {
        await AddAsync("Move house");
        var screen = await OpenAsync();

        screen.ToggleCollapsedCommand.Execute(screen.TaskLists.Single());
        screen.ToggleCollapsedCommand.Execute(screen.TaskLists.Single());

        Assert.False(screen.TaskLists.Single().IsCollapsed);
        Assert.Empty(Arrangement.RememberedCollapsed);
    }


    /// <summary>
    /// Each chip carries the count of what it would leave, and that is what makes it worth tapping.
    /// The count was recomputed only when a chip was tapped, so every chip read "0" from the first
    /// paint - on a screen already showing the lists they were counting. Found on a device.
    ///
    /// The count itself was always right when asked for; what was missing was anything telling the
    /// screen to ask again. So this watches the notification rather than reading the property, which
    /// a binding cannot do and a test that only read it would pass either way.
    /// </summary>
    [Fact]
    public async Task Loading_tells_the_chips_to_count_again()
    {
        await AddAsync("Move house", "Shopping");
        var screen = await OpenAsync();

        var recounted = false;
        screen.PropertyChanged += (_, changed) => recounted |= changed.PropertyName == nameof(screen.Filters);
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(recounted);
        Assert.Equal(2, screen.Filters.Single(filter => filter.Status is null).Count);
    }

    /// <summary>
    /// Pinning needs the server, and a phone with no connection has to be told so - the message exists
    /// for exactly that. Nothing on the page was bound to it, so the tap did nothing and said nothing.
    /// Found on a device, with the phone's radios switched off.
    /// </summary>
    [Fact]
    public async Task Pinning_with_no_connection_has_something_to_say()
    {
        await AddAsync("Move house");
        var screen = await OpenAsync();
        _server.IsUnreachable = true;

        var announced = false;
        screen.PropertyChanged += (_, changed) => announced |= changed.PropertyName == nameof(screen.HasMessage);
        await screen.TogglePinCommand.ExecuteAsync(screen.TaskLists.Single());

        Assert.True(screen.HasMessage);
        Assert.NotEmpty(screen.Message);
        // And says so out loud: the page shows it only when it is told the property changed.
        Assert.True(announced);
    }
    /// <summary>
    /// Finding an entry among every list, by a word in it or by what it is filed under - the same
    /// question Orbit.Web's tasks page asks. The phone could only narrow to whole lists by status, so
    /// "where did I write down the car insurance" meant opening them one at a time.
    /// </summary>
    [Fact]
    public async Task An_entry_can_be_found_by_a_word_in_it()
    {
        await AddWithEntriesAsync("Errands", ("Renew the car insurance", []));
        await AddWithEntriesAsync("Groceries", ("Buy milk", []));
        var screen = await OpenAsync();

        screen.ItemSearch = "insurance";

        Assert.Equal(["Errands"], Titles(screen));
        // The card says what answered rather than what is next: a list shown for a match nobody can see
        // reads as a bug.
        Assert.Equal("Renew the car insurance", Assert.Single(screen.TaskLists).NextOrMatched);
    }

    [Fact]
    public async Task An_entry_can_be_found_by_what_it_is_filed_under()
    {
        await AddWithEntriesAsync("Errands", ("Renew the car insurance", ["car"]));
        await AddWithEntriesAsync("Groceries", ("Buy milk", ["shopping"]));
        var screen = await OpenAsync();

        Assert.Equal(["car", "shopping"], screen.Categories.Select(category => category.Name));
        Assert.Equal([1, 1], screen.Categories.Select(category => category.Count));

        screen.ToggleCategoryCommand.Execute(screen.Categories.Single(category => category.Name == "car"));

        Assert.Equal(["Errands"], Titles(screen));
        Assert.True(screen.IsLookingForAnEntry);
    }

    /// <summary>
    /// Two chosen categories mean "either of them" unless the reader says otherwise - which is what
    /// picking a second one usually means.
    /// </summary>
    [Fact]
    public async Task Two_categories_mean_either_of_them_until_told_otherwise()
    {
        await AddWithEntriesAsync("Errands", ("Renew the car insurance", ["car", "money"]));
        await AddWithEntriesAsync("Groceries", ("Buy milk", ["shopping"]));
        var screen = await OpenAsync();

        screen.ToggleCategoryCommand.Execute(screen.Categories.Single(category => category.Name == "car"));
        screen.ToggleCategoryCommand.Execute(screen.Categories.Single(category => category.Name == "shopping"));

        Assert.Equal(["Errands", "Groceries"], Titles(screen).Order());
        Assert.True(screen.IsCategoryRuleWorthAsking);

        screen.MatchesEveryCategory = true;

        // Nothing is both, so nothing is left.
        Assert.Empty(screen.TaskLists);
    }

    [Fact]
    public async Task Clearing_it_brings_every_list_back()
    {
        await AddWithEntriesAsync("Errands", ("Renew the car insurance", ["car"]));
        await AddWithEntriesAsync("Groceries", ("Buy milk", ["shopping"]));
        var screen = await OpenAsync();
        screen.ItemSearch = "insurance";

        screen.ClearItemFilterCommand.Execute(null);

        Assert.Equal(["Errands", "Groceries"], Titles(screen).Order());
        Assert.False(screen.IsLookingForAnEntry);
        Assert.Empty(screen.ItemSearch);
    }

    private static IReadOnlyList<string> Titles(TasksViewModel screen)
        => [.. screen.TaskLists.Select(row => row.Title)];

    /// <summary>
    /// The card's own menu, which is the only way a list leaves the screen without being opened first -
    /// see TasksPage, which is where the question in front of it is asked.
    /// </summary>
    [Fact]
    public async Task Deleting_a_list_from_its_card_takes_it_off_the_screen()
    {
        await AddAsync("Shopping");
        var screen = await OpenAsync();

        await screen.DeleteListCommand.ExecuteAsync(Assert.Single(screen.TaskLists));

        Assert.Empty(screen.TaskLists);
    }

    /// <summary>
    /// Guarded here as well as on the card. The card leaves the entry out, but a view model that took
    /// the press anyway would delete somebody else's list the moment anything else called it - which
    /// is the failure worth a test rather than the drawing.
    /// </summary>
    [Fact]
    public async Task A_list_shared_with_me_is_not_this_readers_to_delete()
    {
        await AddAsync("Shopping");
        var screen = await OpenAsync();
        var theirs = Assert.Single(screen.TaskLists) with { IsSharedWithMe = true };

        await screen.DeleteListCommand.ExecuteAsync(theirs);

        Assert.Single(screen.TaskLists);
    }

    private async Task AddAsync(params string[] titles)
    {
        foreach (var title in titles)
        {
            await _taskLists.CreateAsync(title, TaskListRow.NoItems);

            // Apart in time, so "most recently changed" - what the repository reads them back in - is
            // the order they were made in, and a test about arranging them is not a test about a tie.
            _clock.Advance(TimeSpan.FromMinutes(1));
        }
    }

    /// <summary>A list with entries on it, each filed under whatever the test is about.</summary>
    private async Task AddWithEntriesAsync(string title, params (string Description, string[] Categories)[] entries)
    {
        var created = await _taskLists.CreateAsync(title, TaskListRow.NoItems);
        await _taskLists.UpdateAsync(
            created.LocalId,
            new TaskListContent(
                title,
                [.. entries.Select(entry => new TaskItemDto(
                    Guid.NewGuid(), entry.Description, null, false, null, "Push", false, "Push",
                    new TimeOnly(9, 0), Categories: entry.Categories))],
                IsGroup: false,
                Priority: "Normal"));

        _clock.Advance(TimeSpan.FromMinutes(1));
    }

    private void CompleteOnTheServer(string title)
    {
        var taskList = _server.TaskLists.Single(list => list.Title == title);
        _clock.Advance(TimeSpan.FromMinutes(1));
        _server.ReplaceForTest(taskList with { Status = "Completed", UpdatedAtUtc = _clock.GetUtcNow() });
    }
    private async Task<TasksViewModel> OpenAsync()
    {
        var screen = new TasksViewModel(
            _taskLists,
            new TaskListSynchronizer(
                _localStore, new TasksClient(_server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<TaskListSynchronizer>.Instance),
            new TasksClient(_server.ToHttpClient()), FixedNetworkStatus.Online, Arrangement,
            new PrivateItemGate(new FixedDeviceAuthentication()),
            new SyncState(FixedNetworkStatus.Online, _clock), new RecordingScreenNavigator(),
            new Translations(new InMemoryLanguageStore()));

        await screen.LoadCommand.ExecuteAsync(null);
        return screen;
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Tests.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Security;
using Orbit.Mobile.Screens.Dashboard;
using System.Net;
using Orbit.Core.Permissions;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// Where the app opens. Its job is to answer "what is on my plate" in one glance, which makes the
/// choices about what it leaves *out* - finished events, sections with nothing in them, requests the
/// reader cannot act on - as much the subject as what it shows.
/// </summary>
public sealed class DashboardScreenTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-08-27T09:00:00Z");

    [Fact]
    public async Task An_account_with_nothing_in_it_says_so()
    {
        using var context = new DashboardContext();
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.True(screen.HasNothing);
        Assert.Empty(screen.Cards);
    }

    [Fact]
    public async Task A_section_with_nothing_in_it_gets_no_card()
    {
        // An empty card costs a phone's screen to say nothing.
        using var context = new DashboardContext();
        await context.AddNoteAsync("Shopping");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal(DashboardCardKind.Notes, Assert.Single(screen.Cards).Kind);
    }

    [Fact]
    public async Task Todays_tasks_and_events_are_counted_and_older_ones_are_not()
    {
        using var context = new DashboardContext();
        await context.AddTaskListAsync("Errands",
            ("Post the parcel", Now, false),
            ("Call the dentist", Now.AddDays(-3), false),
            ("Already done", Now, true));
        await context.AddEventAsync("Standup", Now.AddHours(1));
        await context.AddEventAsync("Last week", Now.AddDays(-7));
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        // Only what is due today and still outstanding: a finished task is not on anybody's plate, and
        // an overdue one belongs to its own list rather than to today's count.
        Assert.Equal(1, screen.Today.TasksDueToday);
        Assert.Equal(1, screen.Today.EventsToday);
    }

    [Fact]
    public async Task Only_chat_requests_waiting_on_the_reader_are_counted()
    {
        // One they sent and nobody has answered is not something they can act on, so counting it would
        // be asking them to do nothing.
        using var context = new DashboardContext();
        await context.AddContactAsync("Wants to talk", requiresMyApproval: true);
        await context.AddContactAsync("Waiting on them", requiresMyApproval: false);
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal(1, screen.Today.PendingChatRequests);
    }

    [Fact]
    public async Task The_calendar_card_shows_everything_soonest_first()
    {
        // Not only what is ahead. Filtering to the future reads as the better idea and was a divergence:
        // Orbit.Web shows the lot, so an account whose events have all been and gone showed a calendar
        // card there and none here.
        using var context = new DashboardContext();
        await context.AddEventAsync("Tomorrow", Now.AddDays(1));
        await context.AddEventAsync("Long gone", Now.AddDays(-10));
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var events = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Upcoming);
        Assert.Equal(["Long gone", "Tomorrow"], events.Rows.Select(row => row.Title));
    }

    [Fact]
    public async Task Chats_are_listed_twice_for_two_different_questions()
    {
        // As Orbit.Web does: "Recent chats" answers who you were just talking to, "Contacts" is a
        // directory. An unanswered request belongs only to the first - it is not a contact yet.
        using var context = new DashboardContext();
        await context.AddContactAsync("Zoe", requiresMyApproval: false);
        await context.AddContactAsync("Wants to talk", requiresMyApproval: true);
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var recent = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.RecentChats);
        var directory = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Contacts);
        Assert.Equal("Wants to talk", recent.Rows[0].Title);
        Assert.Equal("Zoe", Assert.Single(directory.Rows).Title);
    }

    [Fact]
    public async Task The_cards_come_in_the_order_the_web_lays_them_out()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("A note");
        await context.AddTaskListAsync("A list", ("One", null, false));
        await context.AddEventAsync("An event", Now);
        await context.AddContactAsync("Bob", requiresMyApproval: false);
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal(
            [DashboardCardKind.Notes, DashboardCardKind.Tasks, DashboardCardKind.Upcoming,
             DashboardCardKind.RecentChats, DashboardCardKind.Contacts],
            screen.Cards.Select(card => card.Kind));
    }

    [Fact]
    public async Task A_task_list_shows_how_far_through_it_is()
    {
        using var context = new DashboardContext();
        await context.AddTaskListAsync("Errands",
            ("One", null, true), ("Two", null, false), ("Three", null, false));
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var tasks = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Tasks);
        Assert.Equal("1/3", Assert.Single(tasks.Rows).Detail);
    }

    [Fact]
    public async Task Tapping_a_task_list_opens_that_list_rather_than_the_section()
    {
        using var context = new DashboardContext();
        var taskListId = await context.AddTaskListAsync("Errands", ("One", null, false));
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        var tasks = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Tasks);

        await screen.OpenCommand.ExecuteAsync(tasks.Rows[0]);

        Assert.Equal("ShowTaskList", context.Navigator.LastDestination);
        Assert.Equal(taskListId, context.Navigator.LastTaskListId);
    }

    [Fact]
    public async Task Tapping_a_contact_opens_the_conversation_with_them()
    {
        using var context = new DashboardContext();
        var userId = await context.AddContactAsync("Bob", requiresMyApproval: false);
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        var contacts = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.RecentChats);

        await screen.OpenCommand.ExecuteAsync(contacts.Rows[0]);

        Assert.Equal("ShowConversation", context.Navigator.LastDestination);
        Assert.Equal(userId, context.Navigator.LastContact!.UserId);
    }

    [Fact]
    public async Task A_card_shows_a_few_rows_and_the_real_total()
    {
        using var context = new DashboardContext();
        for (var index = 0; index < 9; index++)
        {
            await context.AddNoteAsync($"Note {index}");
        }

        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        // The count is the truth about the section; the rows are just a way in.
        var notes = Assert.Single(screen.Cards);
        Assert.Equal("9", notes.Count);
        Assert.Equal(6, notes.Rows.Count);
    }

    [Fact]
    public async Task An_untitled_thing_is_still_recognisable()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("   ");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        // A blank row would be untappable in practice - nothing to aim at.
        Assert.Equal("Untitled", Assert.Single(Assert.Single(screen.Cards).Rows).Title);
    }

    /// <summary>
    /// A note this device cannot open has no title to show - it is sealed with the rest of it - so
    /// "Untitled" would claim it has none, which is a different thing. The row is still there, and its
    /// own screen says which of the two reasons it is.
    /// </summary>
    [Fact]
    public async Task A_note_this_device_cannot_open_is_named_as_private_rather_than_untitled()
    {
        using var context = new DashboardContext();
        await context.AddSealedNoteAsync();
        await context.PrivateItems.TryUnlockAsync();
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal("Private", Assert.Single(Assert.Single(screen.Cards).Rows).Title);
    }

    /// <summary>
    /// The dashboard used to read the local store and stop there, on the assumption that each section
    /// keeps itself current. A section only does that once its own screen has been opened, so after a
    /// sign-in - or after the cache was emptied - the landing screen sat empty until the reader had
    /// visited Notes, then Tasks, then the calendar, each filling in its own row. Found by walking the
    /// app: every other screen had the account's data and the dashboard had none of it.
    /// </summary>
    [Fact]
    public async Task The_dashboard_fetches_what_this_phone_has_not_seen_yet()
    {
        using var context = new DashboardContext();
        context.NotesServer.AddNote("Only on the server");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var notes = Assert.Single(screen.Cards);
        Assert.Equal("Only on the server", Assert.Single(notes.Rows).Title);
    }

    /// <summary>
    /// A locked section is not a broken one. The chat synchroniser answers false for a refusal exactly
    /// as it does for a dropped connection, so before this the dashboard of an account without chat put
    /// "couldn't sync" in the corner while everything it is allowed to have was perfectly in step.
    /// </summary>
    [Fact]
    public async Task An_account_without_chat_is_not_told_the_sync_failed()
    {
        using var context = new DashboardContext();
        await context.LockToAsync(ApplicationPermission.Location);
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal(SyncCondition.Synced, context.SyncState.Condition);
    }

    [Fact]
    public async Task An_account_without_chat_is_shown_no_conversations()
    {
        using var context = new DashboardContext();
        await context.AddContactAsync("Anna", requiresMyApproval: false);
        await context.LockToAsync(ApplicationPermission.Location);
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.DoesNotContain(screen.Cards, card => card.Kind is DashboardCardKind.RecentChats or DashboardCardKind.Contacts);
    }

    /// <summary>
    /// Pinning lifts a card without disturbing the order of the rest - the cards are built in the order
    /// the web lays them out, and pinning moves one of them rather than sorting them all afresh.
    /// </summary>
    [Fact]
    public async Task A_pinned_card_comes_first()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("Shopping");
        await context.AddTaskListAsync("Trip", ("pack", null, false));
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal(DashboardCardKind.Notes, screen.Cards[0].Kind);

        screen.TogglePinCommand.Execute(screen.Cards.Single(card => card.Kind == DashboardCardKind.Tasks));

        Assert.Equal(DashboardCardKind.Tasks, screen.Cards[0].Kind);
        Assert.True(screen.Cards[0].IsPinned);
    }

    [Fact]
    public async Task Unpinning_puts_it_back_where_it_was()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("Shopping");
        await context.AddTaskListAsync("Trip", ("pack", null, false));
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        var tasks = screen.Cards.Single(card => card.Kind == DashboardCardKind.Tasks);
        screen.TogglePinCommand.Execute(tasks);
        screen.TogglePinCommand.Execute(screen.Cards[0]);

        Assert.Equal(DashboardCardKind.Notes, screen.Cards[0].Kind);
    }

    /// <summary>It is one page's layout on one device, so it has to survive the page being rebuilt.</summary>
    [Fact]
    public async Task A_pin_survives_the_screen_being_opened_again()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("Shopping");
        await context.AddTaskListAsync("Trip", ("pack", null, false));
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.TogglePinCommand.Execute(screen.Cards.Single(card => card.Kind == DashboardCardKind.Tasks));

        var reopened = context.Open();
        await reopened.LoadCommand.ExecuteAsync(null);

        Assert.Equal(DashboardCardKind.Tasks, reopened.Cards[0].Kind);
    }

    /// <summary>
    /// Orbit.Web draws a dot in the event's own colour beside it here, and nothing beside a note. The
    /// colour travels as it was chosen; an event without one leaves the paint to the theme, which is
    /// not the view model's to decide - see EventColourConverter.
    /// </summary>
    [Fact]
    public async Task An_event_carries_its_colour_onto_the_dashboard()
    {
        using var context = new DashboardContext();
        await context.AddEventAsync("Standup", Now.AddHours(1), colour: "#2B7BB9");
        await context.AddNoteAsync("Shopping");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var events = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Upcoming);
        var eventRow = Assert.Single(events.Rows);
        Assert.True(eventRow.HasColourDot);
        Assert.Equal("#2B7BB9", eventRow.Colour);

        var notes = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Notes);
        Assert.False(Assert.Single(notes.Rows).HasColourDot);
    }

    /// <summary>An event nobody gave a colour still gets the dot - the accent stands in for the colour.</summary>
    [Fact]
    public async Task An_event_with_no_colour_still_gets_a_dot()
    {
        using var context = new DashboardContext();
        await context.AddEventAsync("Standup", Now.AddHours(1));
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var events = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Upcoming);
        var eventRow = Assert.Single(events.Rows);
        Assert.True(eventRow.HasColourDot);
        Assert.Null(eventRow.Colour);
    }

    /// <summary>
    /// The bar Orbit.Web fills beside the count, at the same fraction. One of three done is a third,
    /// and the count beside it still reads what it always did.
    /// </summary>
    [Fact]
    public async Task A_task_list_carries_how_far_through_it_is()
    {
        using var context = new DashboardContext();
        await context.AddTaskListAsync("Errands",
            ("Post the parcel", Now, true),
            ("Call the dentist", Now, false),
            ("Buy milk", Now, false));
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var tasks = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Tasks);
        var row = Assert.Single(tasks.Rows);
        Assert.True(row.HasProgress);
        Assert.Equal(1.0 / 3, row.Progress, 5);
        Assert.Equal("1/3", row.Detail);
    }

    /// <summary>
    /// A list with nothing in it gets no bar. Drawn at zero it would say somebody has work they have
    /// not started, which is the opposite of what an empty list means.
    /// </summary>
    [Fact]
    public async Task A_list_with_nothing_in_it_gets_no_bar()
    {
        using var context = new DashboardContext();
        await context.AddTaskListAsync("Someday");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var tasks = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Tasks);
        Assert.False(Assert.Single(tasks.Rows).HasProgress);
    }

    /// <summary>
    /// The "Show on the dashboard" menu Orbit.Web keeps under the page's overflow. Putting a part away
    /// takes it off the page and remembers it, so it is still away the next time the screen opens.
    /// </summary>
    [Fact]
    public async Task A_part_put_away_leaves_the_dashboard_and_stays_away()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("Shopping");
        await context.AddEventAsync("Standup", Now.AddHours(1));
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.ToggleCardChoicesCommand.Execute(null);
        screen.ToggleCardShownCommand.Execute(
            screen.CardChoices.Single(choice => choice.Kind == DashboardCardKind.Notes));

        Assert.DoesNotContain(screen.Cards, card => card.Kind == DashboardCardKind.Notes);
        Assert.Contains(screen.Cards, card => card.Kind == DashboardCardKind.Upcoming);

        var reopened = context.Open();
        await reopened.LoadCommand.ExecuteAsync(null);
        Assert.DoesNotContain(reopened.Cards, card => card.Kind == DashboardCardKind.Notes);
    }

    /// <summary>
    /// Every part is listed, including the ones with nothing in them - a card that is both empty and
    /// put away would otherwise have no way back onto the page.
    /// </summary>
    [Fact]
    public async Task The_menu_lists_every_part_even_the_empty_ones()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("Shopping");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        screen.ToggleCardChoicesCommand.Execute(null);

        Assert.Equal(Enum.GetValues<DashboardCardKind>().Length, screen.CardChoices.Count);
        Assert.All(screen.CardChoices, choice => Assert.True(choice.IsShown));
    }

    /// <summary>A part put away and brought back is on the page again, without reloading anything.</summary>
    [Fact]
    public async Task A_part_brought_back_returns_to_the_dashboard()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("Shopping");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        screen.ToggleCardChoicesCommand.Execute(null);

        var notes = screen.CardChoices.Single(choice => choice.Kind == DashboardCardKind.Notes);
        screen.ToggleCardShownCommand.Execute(notes);
        screen.ToggleCardShownCommand.Execute(
            screen.CardChoices.Single(choice => choice.Kind == DashboardCardKind.Notes));

        Assert.Contains(screen.Cards, card => card.Kind == DashboardCardKind.Notes);
    }

    /// <summary>
    /// Narrowing a card to what is pinned, which is what Orbit.Web's card menu offers for notes and
    /// lists. The count has to narrow with the rows: a card reading "2" while showing one would be
    /// worse than no count at all.
    /// </summary>
    [Fact]
    public async Task A_card_narrowed_to_pinned_shows_only_pinned_and_counts_only_those()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("Shopping");
        var pinned = await context.AddNoteAsync("Rent");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        // Pinned after the first load, as a reader would: the pin is the server's to confirm, and a
        // sync would otherwise put it back the way the server still remembers it.
        await context.PinNoteAsync(pinned);

        await screen.ChooseFilterCommand.ExecuteAsync(
            screen.FilterChoicesFor(DashboardCardKind.Notes)
                .Single(choice => choice.Filter == DashboardCardFilter.Pinned));

        var notes = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Notes);
        Assert.Equal("Rent", Assert.Single(notes.Rows).Title);
        Assert.Equal("1", notes.Count);
    }

    /// <summary>Widening a card again brings the rest back.</summary>
    [Fact]
    public async Task A_card_widened_again_shows_everything()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("Shopping");
        var pinned = await context.AddNoteAsync("Rent");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);
        await context.PinNoteAsync(pinned);
        await screen.ChooseFilterCommand.ExecuteAsync(
            screen.FilterChoicesFor(DashboardCardKind.Notes)
                .Single(choice => choice.Filter == DashboardCardFilter.Pinned));

        await screen.ChooseFilterCommand.ExecuteAsync(
            screen.FilterChoicesFor(DashboardCardKind.Notes)
                .Single(choice => choice.Filter == DashboardCardFilter.All));

        Assert.Equal(2, Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Notes).Rows.Count);
    }

    /// <summary>
    /// The choice outlives the screen, like the parts put away. Asserted on the menu rather than on the
    /// rows: what is stored is the filter, and whether a note is pinned is the server's to say.
    /// </summary>
    [Fact]
    public async Task A_cards_filter_is_remembered_between_visits()
    {
        using var context = new DashboardContext();
        await context.AddNoteAsync("Shopping");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.ChooseFilterCommand.ExecuteAsync(
            screen.FilterChoicesFor(DashboardCardKind.Notes)
                .Single(choice => choice.Filter == DashboardCardFilter.Pinned));

        var reopened = context.Open();
        Assert.True(reopened.FilterChoicesFor(DashboardCardKind.Notes)
            .Single(choice => choice.Filter == DashboardCardFilter.Pinned).IsChosen);
    }

    /// <summary>
    /// Only the cards whose items can be narrowed get a menu. Chats and contacts hold things with
    /// neither a pin nor a priority, so offering one would open an empty sheet.
    /// </summary>
    [Fact]
    public void Only_the_cards_with_something_to_narrow_offer_a_menu()
    {
        using var context = new DashboardContext();
        var screen = context.Open();

        Assert.NotEmpty(screen.FilterChoicesFor(DashboardCardKind.Notes));
        Assert.NotEmpty(screen.FilterChoicesFor(DashboardCardKind.Upcoming));
        Assert.Empty(screen.FilterChoicesFor(DashboardCardKind.Contacts));
        Assert.Empty(screen.FilterChoicesFor(DashboardCardKind.RecentChats));
    }

    /// <summary>
    /// Somebody sharing where they are, which Orbit.Web puts on its dashboard and the phone did not -
    /// and which is worth more here, where the reader is the one out and about.
    /// </summary>
    [Fact]
    public async Task Somebody_sharing_their_position_is_on_the_dashboard()
    {
        using var context = new DashboardContext();
        context.SomebodySharesTheirPosition("Bob", isContinuous: true);
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var card = screen.Cards.Single(candidate => candidate.Kind == DashboardCardKind.SharedLocations);
        var row = Assert.Single(card.Rows);
        Assert.Equal("Bob", row.Title);
        // Whether it keeps coming or was sent once, in the same two words the web uses.
        Assert.Equal("live", row.Detail);
    }

    [Fact]
    public async Task A_position_sent_once_says_so()
    {
        using var context = new DashboardContext();
        context.SomebodySharesTheirPosition("Bob");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.Equal(
            "sent once",
            Assert.Single(screen.Cards.Single(card => card.Kind == DashboardCardKind.SharedLocations).Rows).Detail);
    }

    /// <summary>A position is a pin, and the map is the only place one can be looked at.</summary>
    [Fact]
    public async Task Opening_one_goes_to_the_map()
    {
        using var context = new DashboardContext();
        context.SomebodySharesTheirPosition("Bob");
        var screen = context.Open();
        await screen.LoadCommand.ExecuteAsync(null);

        await screen.OpenCommand.ExecuteAsync(
            screen.Cards.Single(card => card.Kind == DashboardCardKind.SharedLocations).Rows[0]);

        Assert.Equal("ShowMap", context.Navigator.LastDestination);
    }

    /// <summary>Nobody sharing is no card, as every other card on this screen behaves.</summary>
    [Fact]
    public async Task Nobody_sharing_means_no_card_at_all()
    {
        using var context = new DashboardContext();
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        Assert.DoesNotContain(screen.Cards, card => card.Kind == DashboardCardKind.SharedLocations);
    }

    /// <summary>
    /// How much something matters is badged on the row, as Orbit.Web badges the same rows - and only
    /// where it says something: Normal is what everything is unless somebody said otherwise, so marking
    /// every row would mark none of them out.
    /// </summary>
    [Fact]
    public async Task What_matters_more_is_marked_on_the_dashboard()
    {
        using var context = new DashboardContext();
        await context.MarkAsync(await context.AddNoteAsync("Passport"), "High");
        await context.AddNoteAsync("Shopping");
        await context.MarkAsync(await context.AddTaskListAsync("Move house"), "Low");
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        var notes = screen.Cards.Single(card => card.Kind == DashboardCardKind.Notes);
        Assert.Equal("High", notes.Rows.Single(row => row.Title == "Passport").Priority);
        Assert.True(notes.Rows.Single(row => row.Title == "Passport").HasPriority);
        Assert.False(notes.Rows.Single(row => row.Title == "Shopping").HasPriority);

        var tasks = screen.Cards.Single(card => card.Kind == DashboardCardKind.Tasks);
        Assert.Equal("Low", Assert.Single(tasks.Rows).Priority);
    }

    private sealed class DashboardContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(Now);
        private readonly LocalNoteRepository _notes;
        private readonly LocalTaskListRepository _taskLists;
        private readonly LocalCalendarEventRepository _calendarEvents;
        private readonly ChatRepository _chat;
        private readonly EverythingSynchronizer _synchronizer;
        private readonly SyncState _syncState;
        private readonly FakeUsersServer _permissionServer;
        private readonly UserPermissions _permissions;
        private FakeChatServer _chatServer = null!;

        public DashboardContext()
        {
            var network = FixedNetworkStatus.Online;
            _notes = new LocalNoteRepository(_localStore, _clock, network, PrivateContent.WithoutAKey());
            _taskLists = new LocalTaskListRepository(_localStore, _clock, network, PrivateContent.WithoutAKey());
            _calendarEvents = new LocalCalendarEventRepository(_localStore, _clock, network);
            _chat = new ChatRepository(_localStore, _clock);
            _syncState = new SyncState(network, _clock);
            NotesServer = new FakeNotesServer(_clock);
            _permissionServer = new FakeUsersServer();
            _permissionServer.Granted.AddRange(
                Enum.GetValues<ApplicationPermission>().Select(permission => permission.ToString()));
            _permissions = UnlockedPermissions.For(_localStore, _permissionServer);
            LocationServer = new FakeLocationServer(_clock) { CallerUserId = _ownUserId };
            _synchronizer = AssembleSynchronizer();
        }

        /// <summary>The one server a test reaches into, to put something on it the phone has not seen.</summary>
        public FakeNotesServer NotesServer { get; }

        public SyncState SyncState => _syncState;

        /// <summary>Which cards this reader keeps at the top - see IDashboardPinStore.</summary>
        public InMemoryDashboardPinStore Pins { get; } = new();

        /// <summary>Which parts this reader has put away - see IDashboardCardPreferenceStore.</summary>
        public InMemoryDashboardCardPreferenceStore Visibility { get; } = new();

        /// <summary>
        /// Narrows this account to what it has actually unlocked, and makes the chat server refuse the
        /// way the real one does - a locked endpoint answers 403 rather than simply having nothing.
        /// </summary>
        public async Task LockToAsync(params ApplicationPermission[] granted)
        {
            _permissionServer.Granted.Clear();
            _permissionServer.Granted.AddRange(granted.Select(permission => permission.ToString()));

            if (!granted.Contains(ApplicationPermission.Chat))
            {
                _chatServer.RefuseEverythingWith = HttpStatusCode.Forbidden;
            }

            await _permissions.RefreshAsync();
        }

        /// <summary>
        /// A server behind every feature. The dashboard synchronises all of them on load, so leaving any
        /// one out would have the screen under test talk to something that is not there.
        /// </summary>
        private EverythingSynchronizer AssembleSynchronizer()
        {
            var gate = new SyncGate();
            var ownUserId = Guid.NewGuid();
            var sessionStore = new SessionStore(new InMemorySessionStorage(
                new UserSession("access", "refresh", ownUserId, "me@orbit.example", "Me")));
            _chatServer = new FakeChatServer(_clock) { CallerUserId = ownUserId };
            var usersClient = new UsersClient(new FakeUsersServer().ToHttpClient());
            var chatClient = new ChatClient(_chatServer.ToHttpClient());
            var encryptionKeys = new OwnEncryptionKeyProvider(
                new InMemoryChatKeyStorage(), new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance);

            return new EverythingSynchronizer(
                new NoteSynchronizer(
                    _localStore, new NotesClient(NotesServer.ToHttpClient()), _clock, gate,
                    NullLogger<NoteSynchronizer>.Instance),
                new TaskListSynchronizer(
                    _localStore, new TasksClient(new FakeTasksServer(_clock).ToHttpClient()), _clock, gate,
                    NullLogger<TaskListSynchronizer>.Instance),
                new CalendarEventSynchronizer(
                    _localStore, new CalendarClient(new FakeCalendarServer(_clock).ToHttpClient()), _clock, gate,
                    new PendingCalendarLinkResolver(_clock, NullLogger<PendingCalendarLinkResolver>.Instance),
                    NullLogger<CalendarEventSynchronizer>.Instance),
                new WarehouseSynchronizer(
                    _localStore, new InventoryClient(new FakeInventoryServer(_clock).ToHttpClient()), _clock, gate,
                    NullLogger<WarehouseSynchronizer>.Instance),
                new ChatSynchronizer(
                    _chat, chatClient, usersClient,
                    new EncryptedChatMessageSender(
                        _chat, chatClient, new ChatDirectoryReader(chatClient, usersClient, sessionStore),
                        encryptionKeys, new SyncGate(), NullLogger<EncryptedChatMessageSender>.Instance),
                    NullLogger<ChatSynchronizer>.Instance),
                _permissions);
        }

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>Where somebody sharing a position comes from - see the "Shared with you" card.</summary>
        private readonly Guid _ownUserId = Guid.NewGuid();

        public FakeLocationServer LocationServer { get; }

        private readonly FakeUsersServer _users = new();

        /// <summary>
        /// Somebody sharing where they are. The position itself is unreadable here, which is enough:
        /// the card says who is sharing and whether it keeps coming, not where they are.
        /// </summary>
        public void SomebodySharesTheirPosition(string displayName, bool isContinuous = false)
        {
            var sharerUserId = Guid.NewGuid();
            _users.Add(sharerUserId, displayName, publicKeyBase64: null);
            LocationServer.AddIncomingShare(
                sharerUserId, "AAAAAAAAAAAAAAAAAAAAAA==", "AAAAAAAAAAAAAAAA", isContinuous);
        }

        /// <summary>
        /// With the chat key unlocked, since a shared position is sealed with the same key - locked, there
        /// is nothing to read and the card stays away, which is its own test.
        /// </summary>
        private SharedLocations SharedPositions()
        {
            var keyStorage = new InMemoryChatKeyStorage();
            var vectors = BrowserVectorsFile.Read();
            using (var own = ChatIdentity.FromBackup(vectors.Alice.Backup, vectors.BackupPassword)!)
            {
                keyStorage.WritePrivateKeyJwkAsync(_ownUserId, own.ExportPrivateKeyJwk()).GetAwaiter().GetResult();
            }

            var sessionStore = new SessionStore(new InMemorySessionStorage(
                new UserSession("access", "refresh", _ownUserId, "me@orbit.example", "Me")));
            return new SharedLocations(
                new LocationClient(LocationServer.ToHttpClient()),
                new UsersClient(_users.ToHttpClient()),
                new OwnEncryptionKeyProvider(
                    keyStorage, new EncryptionKeyClient(new FakeEncryptionKeyServer().ToHttpClient()),
                    sessionStore, NullLogger<OwnEncryptionKeyProvider>.Instance),
                NullLogger<SharedLocations>.Instance);
        }

        /// <summary>One for the whole context, so a test can unlock private things before opening.</summary>
        public PrivateItemGate PrivateItems { get; } = new(new FixedDeviceAuthentication());

        public DashboardViewModel Open()
            => new(_notes, _taskLists, _calendarEvents, _chat, _clock, new Translations(new InMemoryLanguageStore()),
                PrivateItems, _synchronizer, _syncState, _permissions,
                Pins, Visibility, SharedPositions(), Navigator);

        public async Task<Guid> AddNoteAsync(string title)
            => (await _notes.CreateAsync(title, [new NoteContentLineDto("Body", false, false)])).LocalId;

        /// <summary>A note this device cannot open, as the sync would bring one down - see LocalNote.IsSealed.</summary>
        public async Task<Guid> AddSealedNoteAsync()
        {
            var localId = await AddNoteAsync(string.Empty);
            await using var dbContext = _localStore.CreateDbContext();
            var note = dbContext.Notes.Single(candidate => candidate.LocalId == localId);
            note.IsPrivate = true;
            note.Title = string.Empty;
            note.Content = [];
            note.EncryptedCiphertext = "AAAA";
            note.EncryptedNonce = "BBBB";
            await dbContext.SaveChangesAsync();
            return localId;
        }

        /// <summary>Marks something as mattering more, the way the picker on its own screen does.</summary>
        public async Task MarkAsync(Guid localId, string priority)
        {
            await using var dbContext = _localStore.CreateDbContext();
            if (dbContext.Notes.FirstOrDefault(note => note.LocalId == localId) is { } note)
            {
                note.Priority = priority;
            }

            if (dbContext.TaskLists.FirstOrDefault(list => list.LocalId == localId) is { } taskList)
            {
                taskList.Priority = priority;
            }

            await dbContext.SaveChangesAsync();
        }

        /// <summary>Keeps a note at the top, which is what the "Pinned" card filter narrows to.</summary>
        public Task PinNoteAsync(Guid localId) => _notes.MarkPinnedAsync(localId, isPinned: true);

        public async Task<Guid> AddTaskListAsync(
            string title, params (string Description, DateTimeOffset? DueUtc, bool IsCompleted)[] items)
        {
            var created = await _taskLists.CreateAsync(
                title,
                items.Select(item => new TaskItemDto(
                    Guid.NewGuid(), item.Description, item.DueUtc, item.IsCompleted, null, "None", false, "None",
                    new TimeOnly(9, 0))).ToList());
            return created.LocalId;
        }

        public async Task AddEventAsync(string title, DateTimeOffset startUtc, string? colour = null)
            => await _calendarEvents.CreateAsync(new CalendarEventDetailsDto(
                title, null, null, colour, startUtc, startUtc.AddHours(1), false, null, [], [], "None", "None"));

        /// <summary>
        /// Put on the server as well as in the local store, because the dashboard now synchronises on
        /// load and the server owns the contact list: a contact only this phone knew about would be
        /// replaced away by the first sync, as it would be on a real device.
        /// </summary>
        public async Task<Guid> AddContactAsync(string displayName, bool requiresMyApproval)
        {
            var userId = Guid.NewGuid();
            _chatServer.Contacts.Add(new Contracts.Chat.ContactDto(
                userId, $"user{userId:N}", displayName, $"{userId:N}@orbit.example", null,
                _clock.GetUtcNow(), requiresMyApproval, IsPendingApprovalFromOtherParty: false));
            await _chat.StoreContactsAsync(_chatServer.Contacts);
            return userId;
        }

        public void Dispose() => _localStore.Dispose();
    }
}

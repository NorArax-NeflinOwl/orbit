using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Api;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Crypto;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
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
            _notes = new LocalNoteRepository(_localStore, _clock, network);
            _taskLists = new LocalTaskListRepository(_localStore, _clock, network);
            _calendarEvents = new LocalCalendarEventRepository(_localStore, _clock, network);
            _chat = new ChatRepository(_localStore, _clock);
            _syncState = new SyncState(network, _clock);
            NotesServer = new FakeNotesServer(_clock);
            _permissionServer = new FakeUsersServer();
            _permissionServer.Granted.AddRange(
                Enum.GetValues<ApplicationPermission>().Select(permission => permission.ToString()));
            _permissions = UnlockedPermissions.For(_localStore, _permissionServer);
            _synchronizer = AssembleSynchronizer();
        }

        /// <summary>The one server a test reaches into, to put something on it the phone has not seen.</summary>
        public FakeNotesServer NotesServer { get; }

        public SyncState SyncState => _syncState;

        /// <summary>Which cards this reader keeps at the top - see IDashboardPinStore.</summary>
        public InMemoryDashboardPinStore Pins { get; } = new();

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
                    NullLogger<CalendarEventSynchronizer>.Instance),
                new WarehouseSynchronizer(
                    _localStore, new InventoryClient(new FakeInventoryServer(_clock).ToHttpClient()), _clock, gate,
                    NullLogger<WarehouseSynchronizer>.Instance),
                new ChatSynchronizer(
                    _chat, chatClient, usersClient,
                    new EncryptedChatMessageSender(
                        _chat, chatClient, new ChatDirectoryReader(chatClient, usersClient, sessionStore),
                        encryptionKeys, NullLogger<EncryptedChatMessageSender>.Instance),
                    NullLogger<ChatSynchronizer>.Instance),
                _permissions);
        }

        public RecordingScreenNavigator Navigator { get; } = new();

        public DashboardViewModel Open()
            => new(_notes, _taskLists, _calendarEvents, _chat, _clock, new Translations(new InMemoryLanguageStore()),
                new PrivateItemGate(new FixedDeviceAuthentication()), _synchronizer, _syncState, _permissions,
                Pins, Navigator);

        public async Task AddNoteAsync(string title)
            => await _notes.CreateAsync(title, [new NoteContentLineDto("Body", false, false)]);

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

        public async Task AddEventAsync(string title, DateTimeOffset startUtc)
            => await _calendarEvents.CreateAsync(new CalendarEventDetailsDto(
                title, null, null, null, startUtc, startUtc.AddHours(1), false, null, [], [], "None", "None"));

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

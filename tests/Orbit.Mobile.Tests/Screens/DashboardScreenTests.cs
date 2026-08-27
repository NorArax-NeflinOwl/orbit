using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Screens.Dashboard;
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
    public async Task The_events_card_looks_forward_rather_than_back()
    {
        using var context = new DashboardContext();
        await context.AddEventAsync("Long gone", Now.AddDays(-10));
        await context.AddEventAsync("Tomorrow", Now.AddDays(1));
        var screen = context.Open();

        await screen.LoadCommand.ExecuteAsync(null);

        // A calendar's worth on a home screen is the next thing; last month's would bury it.
        var events = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Events);
        Assert.Equal("Tomorrow", Assert.Single(events.Rows).Title);
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
        var contacts = Assert.Single(screen.Cards, card => card.Kind == DashboardCardKind.Contacts);

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
        Assert.Equal(4, notes.Rows.Count);
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

    private sealed class DashboardContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(Now);
        private readonly LocalNoteRepository _notes;
        private readonly LocalTaskListRepository _taskLists;
        private readonly LocalCalendarEventRepository _calendarEvents;
        private readonly ChatRepository _chat;

        public DashboardContext()
        {
            var network = FixedNetworkStatus.Online;
            _notes = new LocalNoteRepository(_localStore, _clock, network);
            _taskLists = new LocalTaskListRepository(_localStore, _clock, network);
            _calendarEvents = new LocalCalendarEventRepository(_localStore, _clock, network);
            _chat = new ChatRepository(_localStore, _clock);
        }

        public RecordingScreenNavigator Navigator { get; } = new();

        public DashboardViewModel Open()
            => new(_notes, _taskLists, _calendarEvents, _chat, _clock, Navigator);

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
        /// Kept and re-stored together, because StoreContactsAsync replaces the whole list - the server
        /// owns it, so a second call with one contact means the reader now has exactly one contact.
        /// </summary>
        private readonly List<Contracts.Chat.ContactDto> _contacts = [];

        public async Task<Guid> AddContactAsync(string displayName, bool requiresMyApproval)
        {
            var userId = Guid.NewGuid();
            _contacts.Add(new Contracts.Chat.ContactDto(
                userId, $"user{userId:N}", displayName, $"{userId:N}@orbit.example", null,
                _clock.GetUtcNow(), requiresMyApproval, IsPendingApprovalFromOtherParty: false));
            await _chat.StoreContactsAsync(_contacts);
            return userId;
        }

        public void Dispose() => _localStore.Dispose();
    }
}

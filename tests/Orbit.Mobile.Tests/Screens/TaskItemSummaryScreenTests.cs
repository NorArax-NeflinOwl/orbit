using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Chat;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Api;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Location;
using Orbit.Mobile.Screens.Tasks;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens;

/// <summary>
/// One entry on its own: what it is, when it is, and where. This is what a deadline with a place opens
/// as from the calendar - a checklist answers "where is it?" with a row of text and a tick box, which
/// is no answer for somewhere you have to get to.
///
/// What is worth guarding is where the place comes from. An entry tied to an event takes it from the
/// event, which is where the coordinates are kept; an entry with an address of its own has only words,
/// and those have to be looked up.
/// </summary>
public sealed class TaskItemSummaryScreenTests
{
    [Fact]
    public async Task It_says_what_the_entry_is_and_when_it_is()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel", due: new DateTime(2026, 8, 20, 17, 0, 0));

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Collect the parcel", screen.Description);
        Assert.Equal("Errands", screen.TaskListTitle);
        Assert.Contains("2026", screen.When);
        Assert.False(screen.IsCompleted);
    }

    /// <summary>
    /// How far down its list the entry stands, as the design's foot line says it - an entry read away
    /// from its list still says where on it it is.
    /// </summary>
    [Fact]
    public async Task It_says_where_on_its_list_the_entry_stands()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel", entriesBefore: 1, entriesAfter: 3);

        var screen = await context.OpenAsync(opened);

        Assert.Equal("2 of 5", screen.Position);
    }

    /// <summary>An entry can lose its date and still be looked at, which is not the same as having none said.</summary>
    [Fact]
    public async Task An_entry_with_no_date_says_so()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel", due: null);

        var screen = await context.OpenAsync(opened);

        Assert.NotEmpty(screen.When);
    }

    [Fact]
    public async Task An_address_of_its_own_is_looked_up_and_pinned()
    {
        using var context = new ScreenContext();
        context.Places = new[] { new { lat = "54.3520", lon = "18.6466", display_name = "Długa 4, Gdańsk" } };
        var opened = await context.AddEntryAsync("Collect the parcel", at: "Długa 4, Gdańsk");

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Długa 4, Gdańsk", screen.Where);
        Assert.NotNull(screen.Pin);
        Assert.Equal(54.3520, screen.Pin.Latitude, precision: 4);
        Assert.True(screen.HasPin);
        Assert.False(screen.IsPlaceUnknown);
    }

    /// <summary>
    /// An address nobody can find stays as the words somebody typed rather than becoming a pin in the
    /// wrong country - the same line Orbit.Web draws.
    /// </summary>
    [Fact]
    public async Task An_address_that_cannot_be_found_keeps_the_words_and_says_so()
    {
        using var context = new ScreenContext();
        context.Places = Array.Empty<object>();
        var opened = await context.AddEntryAsync("Collect the parcel", at: "Nowhere at all");

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Nowhere at all", screen.Where);
        Assert.Null(screen.Pin);
        Assert.True(screen.IsPlaceUnknown);
    }

    /// <summary>
    /// Tied to an event, the event holds the place - one address rather than two that can disagree. No
    /// lookup happens at all, which is the point: the coordinates are already known.
    /// </summary>
    [Fact]
    public async Task An_entry_tied_to_an_event_takes_the_place_from_the_event()
    {
        using var context = new ScreenContext();
        var eventId = await context.AddEventAsync("Dentist", "Wały Piastowskie 1, Gdańsk", 54.3540, 18.6560);
        var opened = await context.AddEntryAsync("Dentist", tiedTo: eventId);

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Wały Piastowskie 1, Gdańsk", screen.Where);
        Assert.NotNull(screen.Pin);
        Assert.Equal(54.3540, screen.Pin.Latitude, precision: 4);
        Assert.Equal(0, context.LookupCount);
    }

    /// <summary>
    /// What the appointment says about itself and who is coming, shown above the map as the browser
    /// shows them. Both live on the event, so an entry that is only a deadline has neither - which is
    /// why each of the two rows hides itself rather than standing empty.
    /// </summary>
    [Fact]
    public async Task An_entry_tied_to_an_event_says_what_it_is_about_and_who_is_coming()
    {
        using var context = new ScreenContext();
        var anna = Guid.NewGuid();
        await context.AddContactAsync(anna, "Anna");
        var eventId = await context.AddEventAsync(
            "Dentist", "Wały Piastowskie 1, Gdańsk", 54.3540, 18.6560,
            description: "Bring the x-rays", guests: [anna]);
        var opened = await context.AddEntryAsync("Dentist", tiedTo: eventId);

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Bring the x-rays", screen.AppointmentDescription);
        Assert.True(screen.HasAppointmentDescription);
        Assert.Equal("Anna", screen.Guests);
        Assert.True(screen.HasGuests);
    }

    [Fact]
    public async Task An_entry_that_is_only_a_deadline_says_neither()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel");

        var screen = await context.OpenAsync(opened);

        Assert.False(screen.HasAppointmentDescription);
        Assert.False(screen.HasGuests);
    }

    /// <summary>
    /// An event this phone has not got leaves the entry's own words standing rather than the screen
    /// empty - a tie is to something that may not have arrived yet.
    /// </summary>
    [Fact]
    public async Task An_entry_tied_to_an_event_this_phone_has_not_got_falls_back_to_its_own_words()
    {
        using var context = new ScreenContext();
        context.Places = Array.Empty<object>();
        var opened = await context.AddEntryAsync("Dentist", tiedTo: Guid.NewGuid(), at: "Wały Piastowskie 1");

        var screen = await context.OpenAsync(opened);

        Assert.Equal("Wały Piastowskie 1", screen.Where);
    }

    [Fact]
    public async Task An_entry_with_nowhere_at_all_says_that_rather_than_nothing()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel");

        var screen = await context.OpenAsync(opened);

        Assert.NotEmpty(screen.Where);
        Assert.Null(screen.Pin);
        // Nothing was written down, so nothing failed to be found - there is nothing to apologise for.
        Assert.False(screen.IsPlaceUnknown);
    }

    /// <summary>
    /// Crossed off and saved away on another device, or the whole list deleted: there is nothing to
    /// show, and a screen that stayed blank would look broken.
    /// </summary>
    [Fact]
    public async Task An_entry_that_is_gone_sends_the_reader_back_to_the_calendar()
    {
        using var context = new ScreenContext();

        var screen = await context.OpenAsync((Guid.NewGuid(), Guid.NewGuid()));

        Assert.Contains("ShowCalendar", context.Navigator.Destinations);
        Assert.Empty(screen.Description);
    }

    /// <summary>The list is where the rest of the entry is changed - the tick is made here.</summary>
    [Fact]
    public async Task It_leads_back_to_the_list_the_entry_is_on()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel");
        var screen = await context.OpenAsync(opened);

        screen.ShowTaskListCommand.Execute(null);

        Assert.Equal(opened.TaskListLocalId, context.Navigator.LastTaskListId);
    }

    /// <summary>
    /// The entry is crossed off where it is read. This screen used to draw the circle and offer no
    /// press, so the one screen about this entry was the one place it could not be finished - somebody
    /// who opened it from the calendar to see when something was due had to go back to the list to tick
    /// it. Written to this phone first, like every other task write, and sent from there.
    /// </summary>
    [Fact]
    public async Task The_entry_is_crossed_off_where_it_is_read()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel");
        var screen = await context.OpenAsync(opened);
        Assert.False(screen.IsCompleted);

        await screen.TickCommand.ExecuteAsync(null);

        Assert.True(screen.IsCompleted);
        Assert.True((await context.StoredEntryAsync(opened)).IsCompleted);
        // And when: recorded with the tick, and said on the screen once there is something to say.
        Assert.NotNull((await context.StoredEntryAsync(opened)).CompletedAtUtc);
        Assert.NotEqual("Not recorded", screen.CompletedOn);
        Assert.Empty(screen.Status);
    }

    /// <summary>
    /// A tick made here writes the whole list back, and it used to write it back without the reader's
    /// own answer about whether the list is finished - so that answer went back to "from the entries" on
    /// every tick made from this screen, while the list screen kept it. See TaskListContent.Completion.
    /// </summary>
    [Fact]
    public async Task A_tick_here_keeps_the_readers_answer_about_whether_the_list_is_finished()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel");
        await context.AnswerWhetherTheListIsFinishedAsync(opened.TaskListLocalId, "Unfinished");
        var screen = await context.OpenAsync(opened);

        await screen.TickCommand.ExecuteAsync(null);

        Assert.Equal("Unfinished", (await context.StoredListAsync(opened.TaskListLocalId)).Completion);
    }

    /// <summary>
    /// "Duplicate", as the design draws the entry's menu: a second entry straight under this one, under
    /// an id of its own, and then its screen. The appointment is not copied - one entry raises it, and
    /// a second pointing at the same event would fight over it (see DuplicateTaskListCommandHandler).
    /// </summary>
    [Fact]
    public async Task Duplicate_puts_a_copy_under_the_entry_without_its_appointment_and_opens_it()
    {
        using var context = new ScreenContext();
        var eventId = Guid.NewGuid();
        var opened = await context.AddEntryAsync("Dentist", tiedTo: eventId, entriesAfter: 1);
        var screen = await context.OpenAsync(opened);

        await screen.DuplicateCommand.ExecuteAsync(null);

        var items = (await context.StoredListAsync(opened.TaskListLocalId)).Items;
        Assert.Equal(3, items.Count);
        Assert.Equal(opened.ItemId, items[0].Id);
        Assert.Equal(eventId, items[0].LinkedCalendarEventId);
        Assert.Equal("Dentist", items[1].Description);
        Assert.NotEqual(opened.ItemId, items[1].Id);
        Assert.Null(items[1].LinkedCalendarEventId);
        Assert.Equal((opened.TaskListLocalId, items[1].Id), context.Navigator.LastTaskItem);
    }

    /// <summary>"Delete item": the entry goes from its list, and the reader lands on that list.</summary>
    [Fact]
    public async Task Delete_item_takes_the_entry_off_its_list_and_opens_the_list()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel", entriesBefore: 1);
        var screen = await context.OpenAsync(opened);

        await screen.DeleteCommand.ExecuteAsync(null);

        var items = (await context.StoredListAsync(opened.TaskListLocalId)).Items;
        Assert.DoesNotContain(items, item => item.Id == opened.ItemId);
        Assert.Single(items);
        Assert.Equal(opened.TaskListLocalId, context.Navigator.LastTaskListId);
    }

    /// <summary>
    /// A list shared to be read is refused by the store, wherever the write is made from - so the press
    /// is answered with why, and the entry is still there.
    /// </summary>
    [Fact]
    public async Task Delete_item_on_a_list_shared_to_read_is_refused_and_said()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel");
        await context.ShareToReadAsync(opened.TaskListLocalId);
        var screen = await context.OpenAsync(opened);

        await screen.DeleteCommand.ExecuteAsync(null);

        Assert.NotEmpty(screen.Status);
        Assert.Contains((await context.StoredListAsync(opened.TaskListLocalId)).Items, item => item.Id == opened.ItemId);
        Assert.DoesNotContain("ShowTaskList", context.Navigator.Destinations);
    }

    /// <summary>A tick is a tick either way round - a box that only fills in is a trap for a misread row.</summary>
    [Fact]
    public async Task A_tick_can_be_taken_back_here_too()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel", isCompleted: true);
        var screen = await context.OpenAsync(opened);

        await screen.TickCommand.ExecuteAsync(null);

        Assert.False(screen.IsCompleted);
        Assert.False((await context.StoredEntryAsync(opened)).IsCompleted);
        // A time for being done goes with the tick.
        Assert.Null((await context.StoredEntryAsync(opened)).CompletedAtUtc);
    }

    /// <summary>
    /// The third answer: an entry that is not going to happen is crossed out rather than ticked off.
    /// One press further round than done - see TickState, which both clients cycle through.
    /// </summary>
    [Fact]
    public async Task The_entry_can_be_crossed_out_rather_than_ticked_off()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel", isCompleted: true);
        var screen = await context.OpenAsync(opened);

        await screen.TickCommand.ExecuteAsync(null);

        Assert.False(screen.IsCompleted);
        Assert.True(screen.IsFailed);
        Assert.True(screen.IsResolved);
        Assert.True((await context.StoredEntryAsync(opened)).IsFailed);
    }

    /// <summary>
    /// A list shared to be read is refused by the store, wherever the write is made from - so the press
    /// is answered with why rather than with nothing, which would read as a press that never registered.
    /// </summary>
    [Fact]
    public async Task A_tick_on_a_list_shared_to_read_is_refused_and_said()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel");
        await context.ShareToReadAsync(opened.TaskListLocalId);
        var screen = await context.OpenAsync(opened);

        await screen.TickCommand.ExecuteAsync(null);

        Assert.NotEmpty(screen.Status);
        Assert.True(screen.HasStatus);
        Assert.False((await context.StoredEntryAsync(opened)).IsCompleted);
    }

    /// <summary>
    /// An entry standing for another list is done when that list is (see LinkedTaskCompletionResolver),
    /// so nothing is written here: the press is taken and answered with where the tick belongs, which
    /// is what the list screen and the browser both answer it with.
    /// </summary>
    [Fact]
    public async Task An_entry_that_stands_for_another_list_says_where_the_tick_belongs()
    {
        using var context = new ScreenContext();
        var kitchen = await context.AddTaskListAsync("Kitchen");
        var opened = await context.AddEntryAsync("Kitchen done", standingFor: kitchen);
        var screen = await context.OpenAsync(opened);

        await screen.TickCommand.ExecuteAsync(null);

        Assert.Contains("Kitchen", screen.Status);
        Assert.False(screen.IsCompleted);
        Assert.False((await context.StoredEntryAsync(opened)).IsCompleted);
    }

    /// <summary>
    /// Crossing something off with no connection still crosses it off - it is written here and queued -
    /// but a reader whose list is shared should know that nobody else can see it yet.
    /// </summary>
    [Fact]
    public async Task A_tick_that_could_not_be_sent_yet_says_it_is_only_on_this_phone()
    {
        using var context = new ScreenContext();
        var opened = await context.AddEntryAsync("Collect the parcel");
        var screen = await context.OpenAsync(opened);
        context.Server.IsUnreachable = true;

        await screen.TickCommand.ExecuteAsync(null);

        Assert.True(screen.IsCompleted);
        Assert.True((await context.StoredEntryAsync(opened)).IsCompleted);
        // Recorded offline too - the phone keeps the time, not the server.
        Assert.NotNull((await context.StoredEntryAsync(opened)).CompletedAtUtc);
        Assert.NotEmpty(screen.Status);
    }

    private sealed class ScreenContext : IDisposable
    {
        private readonly LocalStore _localStore = new();
        private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-08-15T10:00:00Z"));
        private readonly LocalTaskListRepository _taskLists;
        private readonly LocalCalendarEventRepository _events;
        private StubHttpMessageHandler? _nominatim;

        public ScreenContext()
        {
            _taskLists = new LocalTaskListRepository(_localStore, _clock, FixedNetworkStatus.Online, PrivateContent.WithoutAKey());
            _events = new LocalCalendarEventRepository(_localStore, _clock, FixedNetworkStatus.Online);
            Server = new FakeTasksServer(_clock);
            _synchronizer = new TaskListSynchronizer(
                _localStore, new TasksClient(Server.ToHttpClient()), _clock, new SyncGate(),
                NullLogger<TaskListSynchronizer>.Instance);
        }

        /// <summary>Where a tick goes once it is written down - see SynchroniseAsync.</summary>
        public FakeTasksServer Server { get; }

        private readonly TaskListSynchronizer _synchronizer;

        public RecordingScreenNavigator Navigator { get; } = new();

        /// <summary>What the address lookup answers with, in Nominatim's own shape - see PlaceSearch.</summary>
        public object Places { get; set; } = Array.Empty<object>();

        /// <summary>How many times the address was looked up, so "it did not have to be" can be asserted.</summary>
        public int LookupCount => _nominatim?.ReceivedRequests.Count ?? 0;

        /// <param name="standingFor">
        /// The list this entry stands for, by the server id such a tie is stored as - an entry with one
        /// is done when that list is, and is not ticked here at all.
        /// </param>
        /// <param name="entriesBefore">Other entries on the same list above this one.</param>
        /// <param name="entriesAfter">Other entries on the same list below this one.</param>
        public async Task<(Guid TaskListLocalId, Guid ItemId)> AddEntryAsync(
            string description, DateTime? due = null, string at = "", Guid? tiedTo = null,
            bool isCompleted = false, Guid? standingFor = null, int entriesBefore = 0, int entriesAfter = 0)
        {
            var itemId = Guid.NewGuid();
            var dueUtc = due is { } localDue
                ? new DateTimeOffset(localDue, TimeZoneInfo.Local.GetUtcOffset(localDue)).ToUniversalTime()
                : (DateTimeOffset?)null;

            var created = await _taskLists.CreateAsync("Errands",
            [
                .. Enumerable.Range(0, entriesBefore).Select(index => AnotherEntry($"Above {index}")),
                new TaskItemDto(
                    itemId, description, dueUtc, isCompleted, standingFor, "None", false, "None", new TimeOnly(9, 0),
                    "Checklist", at, tiedTo),
                .. Enumerable.Range(0, entriesAfter).Select(index => AnotherEntry($"Below {index}"))
            ]);

            return (created.LocalId, itemId);
        }

        private static TaskItemDto AnotherEntry(string description)
            => new(Guid.NewGuid(), description, null, false, null, "None", false, "None", new TimeOnly(9, 0));

        /// <summary>Another list of this account's, already known to the server - what an entry can stand for.</summary>
        public async Task<Guid> AddTaskListAsync(string title)
        {
            var created = await _taskLists.CreateAsync(title, []);
            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.TaskLists.Single(candidate => candidate.LocalId == created.LocalId);
            stored.ServerId = Guid.NewGuid();
            await dbContext.SaveChangesAsync();
            return stored.ServerId.Value;
        }

        /// <summary>
        /// Turns the list into one somebody handed over to be read - which the store refuses writes to,
        /// wherever they are made from. Written onto the row the way NoteDetailScreenTests does it.
        /// </summary>
        public async Task ShareToReadAsync(Guid taskListLocalId)
        {
            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.TaskLists.Single(candidate => candidate.LocalId == taskListLocalId);
            stored.IsShared = true;
            stored.AccessLevel = "ReadOnly";
            await dbContext.SaveChangesAsync();
        }

        /// <summary>The reader's own answer about whether the list is finished - see LocalTaskList.Completion.</summary>
        public async Task AnswerWhetherTheListIsFinishedAsync(Guid taskListLocalId, string completion)
        {
            await using var dbContext = _localStore.CreateDbContext();
            dbContext.TaskLists.Single(candidate => candidate.LocalId == taskListLocalId).Completion = completion;
            await dbContext.SaveChangesAsync();
        }

        /// <summary>The list as this phone now holds it.</summary>
        public async Task<LocalTaskList> StoredListAsync(Guid taskListLocalId)
            => (await _taskLists.FindAsync(taskListLocalId))!;

        /// <summary>The entry as this phone now holds it - what a tick has to have changed.</summary>
        public async Task<TaskItemDto> StoredEntryAsync((Guid TaskListLocalId, Guid ItemId) opened)
            => (await _taskLists.FindAsync(opened.TaskListLocalId))!
                .Items.Single(item => item.Id == opened.ItemId);

        /// <summary>
        /// An event the server knows about, which is what an entry's tie points at - the tie is stored
        /// as the event's own id.
        /// </summary>
        public async Task<Guid> AddEventAsync(
            string title, string address, double latitude, double longitude,
            string? description = null, IReadOnlyList<Guid>? guests = null)
        {
            var start = _clock.GetUtcNow();
            var created = await _events.CreateAsync(new CalendarEventDetailsDto(
                title, description, new EventLocationDto(address, latitude, longitude), null,
                start, start.AddHours(1), false, null, guests ?? [], [], ReminderNotificationChannel: "None"));

            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.CalendarEvents.Single(candidate => candidate.LocalId == created.LocalId);
            stored.ServerId = Guid.NewGuid();
            await dbContext.SaveChangesAsync();
            return stored.ServerId.Value;
        }

        public async Task<TaskItemSummaryViewModel> OpenAsync((Guid TaskListLocalId, Guid ItemId) opened)
        {
            _nominatim = StubHttpMessageHandler.RespondingWith(Places);
            var screen = new TaskItemSummaryViewModel(
                _taskLists, _events, new PlaceSearch(_nominatim.ToHttpClient()),
                new Translations(new InMemoryLanguageStore()), Navigator,
                new ChatRepository(_localStore, _clock), _synchronizer, _clock);

            screen.Open(opened.TaskListLocalId, opened.ItemId);
            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        /// <summary>Somebody this account has in its contacts, so a guest can be named rather than counted.</summary>
        public async Task AddContactAsync(Guid userId, string displayName)
            => await new ChatRepository(_localStore, _clock).StoreContactsAsync(
                [new ContactDto(userId, displayName, displayName, $"{displayName}@example.com", "public-key", _clock.GetUtcNow(), RequiresApprovalFromCurrentUser: false, IsPendingApprovalFromOtherParty: false)]);

        public void Dispose()
        {
            _nominatim?.Dispose();
            _localStore.Dispose();
        }
    }
}

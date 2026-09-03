using Microsoft.Extensions.Time.Testing;
using Orbit.Contracts.Calendar;
using Orbit.Contracts.Inventories;
using Orbit.Contracts.Notes;
using Orbit.Contracts.Tasks;
using Orbit.Mobile.Authentication;
using Orbit.Mobile.Chat;
using Orbit.Mobile.Data;
using Orbit.Mobile.Localization;
using Orbit.Mobile.Permissions;
using Orbit.Mobile.Screens.Copies;
using Orbit.Mobile.Sync;
using Orbit.Mobile.Tests.TestDoubles;
using Xunit;

namespace Orbit.Mobile.Tests.Screens.Copies;

/// <summary>
/// What happens when somebody comes back with copies in their pocket. The three answers the window
/// offers are the whole of the offline conflict story on this phone, so each is checked for what it
/// leaves behind rather than only for what it says - and for all four kinds, because one screen now
/// decides between a note, a task list, an appointment and an inventory.
/// </summary>
public sealed class CopyReviewScreenTests
{
    [Fact]
    public async Task A_copy_is_shown_beside_what_was_written_into_it()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        var copy = await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");

        var screen = await context.OpenAsync();

        var review = Assert.Single(screen.Reviews);
        Assert.Equal(copy.LocalId, review.LocalId);
        Assert.Equal([("milk", LineChange.Unchanged), ("bread", LineChange.Added)], Described(review.MyChanges));
        Assert.False(review.HasConflict);
    }

    /// <summary>
    /// One screen holds all four, so each row has to say which kind it is - two rows reading "Zakupy"
    /// otherwise give the reader no way of telling a shopping list from a shopping note.
    /// </summary>
    [Fact]
    public async Task Every_kind_lands_in_the_same_window_and_says_which_it_is()
    {
        using var context = new ReviewContext();
        await context.CopyTheNoteAsync((await context.AddSharedNoteAsync("Shopping", "milk")).LocalId, "milk", "bread");
        await context.CopyTheTaskListAsync((await context.AddSharedTaskListAsync("Errands", "post office")).LocalId);
        await context.CopyTheAppointmentAsync((await context.AddSharedAppointmentAsync("Dentist")).LocalId);
        await context.CopyTheInventoryAsync((await context.AddSharedInventoryAsync("Kitchen")).LocalId);

        var screen = await context.OpenAsync();

        Assert.Equal(4, screen.Reviews.Count);
        Assert.Equal(
            [CopyKind.Note, CopyKind.TaskList, CopyKind.CalendarEvent, CopyKind.Inventory],
            screen.Reviews.Select(review => review.Kind).Order());
        Assert.All(screen.Reviews, review => Assert.NotEqual(string.Empty, review.KindDescription));
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
        await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");
        await context.ChangeTheNoteAsync(original.LocalId, "milk", "eggs");

        var screen = await context.OpenAsync();

        var review = Assert.Single(screen.Reviews);
        Assert.True(review.HasConflict);
        Assert.Equal([("milk", LineChange.Unchanged), ("eggs", LineChange.Added)], Described(review.TheirChanges));
    }

    [Fact]
    public async Task Keeping_mine_writes_the_copys_words_onto_the_original_and_drops_the_copy()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepMineCommand.ExecuteAsync(screen.Reviews[0]);

        var stored = await context.Notes.FindAsync(original.LocalId);
        Assert.Equal(["milk", "bread"], stored!.Content.Select(line => line.Text));
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
        await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepMineCommand.ExecuteAsync(screen.Reviews[0]);

        var onTheServer = Assert.Single(context.NoteServer.Notes, note => note.Title == "Team shopping");
        Assert.Equal(["milk", "bread"], onTheServer.Content.Select(line => line.Text));
    }

    [Fact]
    public async Task Keeping_a_task_lists_copy_puts_its_entries_onto_the_list_it_came_from()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedTaskListAsync("Errands", "post office");
        var copy = await context.CopyTheTaskListAsync(original.LocalId);
        await context.WriteIntoTheTaskListAsync(copy.LocalId, "post office", "bank");
        var screen = await context.OpenAsync();

        await screen.KeepMineCommand.ExecuteAsync(screen.Reviews[0]);

        var stored = await context.TaskLists.FindAsync(original.LocalId);
        Assert.Equal(["post office", "bank"], stored!.Items.Select(item => item.Description));
    }

    [Fact]
    public async Task Keeping_an_appointments_copy_moves_the_appointment()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedAppointmentAsync("Dentist");
        var copy = await context.CopyTheAppointmentAsync(original.LocalId);
        await context.MoveTheAppointmentAsync(copy.LocalId, "2026-09-02T11:00:00Z");
        var screen = await context.OpenAsync();

        await screen.KeepMineCommand.ExecuteAsync(screen.Reviews[0]);

        var stored = await context.Appointments.FindAsync(original.LocalId);
        Assert.Equal(DateTimeOffset.Parse("2026-09-02T11:00:00Z"), stored!.Details.StartUtc);
    }

    [Fact]
    public async Task Keeping_an_inventories_copy_writes_its_shelf_back()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedInventoryAsync("Kitchen");
        var copy = await context.CopyTheInventoryAsync(original.LocalId);
        await context.RestockAsync(copy.LocalId, 9);
        var screen = await context.OpenAsync();

        await screen.KeepMineCommand.ExecuteAsync(screen.Reviews[0]);

        var stored = await context.Inventories.FindAsync(original.LocalId);
        Assert.Equal(9, Assert.Single(stored!.Items).Quantity);
    }

    [Fact]
    public async Task Keeping_theirs_leaves_the_original_alone_and_takes_the_copy_away()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepTheirsCommand.ExecuteAsync(screen.Reviews[0]);

        var stored = await context.Notes.FindAsync(original.LocalId);
        Assert.Equal(["milk"], stored!.Content.Select(line => line.Text));
        Assert.Empty(await context.Notes.GetCopiesOfAsync(original.LocalId));
    }

    /// <summary>
    /// A copy is a question, not a thing, until a review answers it - so it goes nowhere. Pushed on
    /// sight, two of the three answers would have to take it off the server again, and the reader would
    /// have watched a duplicate appear and disappear for no reason.
    /// </summary>
    [Fact]
    public async Task A_copy_awaiting_review_is_never_pushed()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        var copy = await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");

        await context.SynchroniseAsync();

        Assert.Empty(context.QueuedFor(copy.LocalId));
        Assert.Single(context.NoteServer.Notes);
    }

    [Fact]
    public async Task Keeping_both_leaves_two_and_stops_asking()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        var copy = await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepBothCommand.ExecuteAsync(screen.Reviews[0]);

        Assert.Empty(screen.Reviews);
        Assert.Equal(2, (await context.Notes.GetAllAsync()).Count);
        Assert.Equal(copy.LocalId, Assert.Single(await context.Notes.GetKeptCopiesAsync()).LocalId);
    }

    /// <summary>Keeping it is what makes it a thing, so that is the point at which it is sent.</summary>
    [Fact]
    public async Task Keeping_both_puts_the_copy_on_the_server_in_its_own_right()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.SynchroniseAsync();
        await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await screen.KeepBothCommand.ExecuteAsync(screen.Reviews[0]);

        Assert.Equal(2, context.NoteServer.Notes.Count);
        Assert.Contains(context.NoteServer.Notes, note => note.Content.Any(line => line.Text == "bread"));
    }

    /// <summary>
    /// A copy carries the original's entry ids so that applying it back replaces words rather than
    /// identity. The moment it becomes a list of its own those ids belong to something else - and two
    /// lists claiming one entry id is exactly what the server has to re-issue ids to resolve.
    /// </summary>
    [Fact]
    public async Task A_kept_task_list_stops_claiming_the_original_lists_entry_ids()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedTaskListAsync("Errands", "post office");
        var copy = await context.CopyTheTaskListAsync(original.LocalId);
        var borrowedIds = copy.Items.Select(item => item.Id).ToList();
        var screen = await context.OpenAsync();

        await screen.KeepBothCommand.ExecuteAsync(screen.Reviews[0]);

        var kept = await context.TaskLists.FindAsync(copy.LocalId);
        Assert.All(kept!.Items, item => Assert.DoesNotContain(item.Id, borrowedIds));
    }

    /// <summary>The same rule for a shelf: a kept inventory's items are new items, not the original's.</summary>
    [Fact]
    public async Task A_kept_inventory_stops_claiming_the_original_shelfs_item_ids()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedInventoryAsync("Kitchen");
        var copy = await context.CopyTheInventoryAsync(original.LocalId);
        var screen = await context.OpenAsync();

        await screen.KeepBothCommand.ExecuteAsync(screen.Reviews[0]);

        var kept = await context.Inventories.FindAsync(copy.LocalId);
        Assert.All(kept!.Items, item => Assert.Null(item.Id));
    }

    /// <summary>
    /// Deleted while the phone was away. There is nothing left to apply the copy over, so the window
    /// says so rather than offering three answers of which two do nothing.
    /// </summary>
    [Fact]
    public async Task A_copy_of_something_that_is_gone_says_so()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");
        await context.Notes.DeleteAsync(original.LocalId);

        var screen = await context.OpenAsync();

        Assert.True(Assert.Single(screen.Reviews).IsOriginalGone);
    }

    /// <summary>
    /// The original is shared and there is still no connection, so the policy refuses the write - and
    /// the copy is left where it is rather than quietly lost.
    /// </summary>
    [Fact]
    public async Task Keeping_mine_while_still_offline_is_refused_and_keeps_the_copy()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();
        context.Network.Becomes(false);

        await screen.KeepMineCommand.ExecuteAsync(screen.Reviews[0]);

        Assert.True(screen.HasMessage);
        Assert.Single(screen.Reviews);
        Assert.Single(await context.Notes.GetCopiesOfAsync(original.LocalId));
    }

    /// <summary>
    /// The reader has to be told which thing they wrote in - a badge saying "1" is a puzzle when two
    /// rows share a title. So the copy announces itself in the feed, by name.
    /// </summary>
    [Fact]
    public async Task Taking_a_copy_says_so_in_the_notification_feed_and_names_what_it_is_a_copy_of()
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedTaskListAsync("Errands", "post office");
        var copy = await context.CopyTheTaskListAsync(original.LocalId);

        var announced = Assert.Single(context.Announcements());
        Assert.True(announced.IsRaisedHere);
        Assert.Equal($"/copies/{copy.LocalId}", announced.Url);
        Assert.Contains("task list", announced.Body);
        Assert.Contains("Errands", announced.BodyArgumentsJson);
    }

    /// <summary>
    /// And it stops saying so once the question has been answered - whichever of the three answers it
    /// was. A feed still advertising a decision already made is worse than not having said it.
    /// </summary>
    [Theory]
    [InlineData("mine")]
    [InlineData("theirs")]
    [InlineData("both")]
    public async Task Answering_a_review_takes_its_notice_away(string answer)
    {
        using var context = new ReviewContext();
        var original = await context.AddSharedNoteAsync("Team shopping", "milk");
        await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");
        var screen = await context.OpenAsync();

        await (answer switch
        {
            "mine" => screen.KeepMineCommand.ExecuteAsync(screen.Reviews[0]),
            "theirs" => screen.KeepTheirsCommand.ExecuteAsync(screen.Reviews[0]),
            _ => screen.KeepBothCommand.ExecuteAsync(screen.Reviews[0])
        });

        Assert.Empty(context.Announcements());
    }

    [Fact]
    public async Task Nothing_taken_offline_means_nothing_to_review()
    {
        using var context = new ReviewContext();
        await context.AddSharedNoteAsync("Team shopping", "milk");

        Assert.True((await context.OpenAsync()).HasNothingToReview);
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
        await context.CopyTheNoteAsync(original.LocalId, "milk", "bread");
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
        private readonly FakeTasksServer _taskServer;
        private readonly FakeCalendarServer _calendarServer;
        private readonly FakeInventoryServer _inventoryServer;
        private readonly EverythingSynchronizer _synchronizer;

        public ReviewContext()
        {
            NoteServer = new FakeNotesServer(_clock);
            _taskServer = new FakeTasksServer(_clock);
            _calendarServer = new FakeCalendarServer(_clock);
            _inventoryServer = new FakeInventoryServer(_clock);

            var privateContent = PrivateContent.WithoutAKey();
            Notes = new LocalNoteRepository(_localStore, _clock, Network, privateContent);
            TaskLists = new LocalTaskListRepository(_localStore, _clock, Network, privateContent);
            Appointments = new LocalCalendarEventRepository(_localStore, _clock, Network);
            Inventories = new LocalInventoryRepository(_localStore, _clock, Network, privateContent);

            var sessionStore = new SessionStore(new InMemorySessionStorage(
                new UserSession("access", "refresh", Guid.NewGuid(), "me@orbit.example", "Ala")));

            _synchronizer = Synchronizers.Against(
                _localStore, new ChatRepository(_localStore, _clock), UnlockedPermissions.For(_localStore),
                sessionStore, NoteServer.ToHttpClient(), _taskServer.ToHttpClient(),
                _calendarServer.ToHttpClient(), _inventoryServer.ToHttpClient());
        }

        public FakeNotesServer NoteServer { get; }

        public LocalNoteRepository Notes { get; }

        public LocalTaskListRepository TaskLists { get; }

        public LocalCalendarEventRepository Appointments { get; }

        public LocalInventoryRepository Inventories { get; }

        public FixedNetworkStatus Network { get; } = FixedNetworkStatus.Online;

        public Task SynchroniseAsync() => _synchronizer.SynchroniseAsync(CancellationToken.None);

        public async Task<LocalNote> AddSharedNoteAsync(string title, params string[] lines)
        {
            var note = await Notes.CreateAsync(title, [.. lines.Select(NoteLine)]);
            await ShareAsync<LocalNote>(note.LocalId);
            return note;
        }

        public async Task<LocalTaskList> AddSharedTaskListAsync(string title, params string[] descriptions)
        {
            var taskList = await TaskLists.CreateAsync(title, [.. descriptions.Select(Entry)]);
            await ShareAsync<LocalTaskList>(taskList.LocalId);
            return taskList;
        }

        public async Task<LocalCalendarEvent> AddSharedAppointmentAsync(string title)
        {
            var appointment = await Appointments.CreateAsync(Details(title));
            await ShareAsync<LocalCalendarEvent>(appointment.LocalId);
            return appointment;
        }

        public async Task<LocalInventory> AddSharedInventoryAsync(string name)
        {
            var inventory = await Inventories.CreateAsync(name);
            await Inventories.UpdateAsync(inventory.LocalId, new InventoryContent(name, [Shelf(4)]));
            await ShareAsync<LocalInventory>(inventory.LocalId);
            return inventory;
        }

        /// <summary>
        /// Makes it somebody else's share, which is the one state the offline policy refuses - see
        /// OfflineEditPolicy. Written straight to the row: no screen can set it, and the server is what
        /// normally does.
        /// </summary>
        private async Task ShareAsync<TEntity>(Guid localId) where TEntity : class, ICopyableForEditing, ISharedState
        {
            await using var dbContext = _localStore.CreateDbContext();
            var stored = dbContext.Set<TEntity>().Single(candidate => candidate.LocalId == localId);
            dbContext.Entry(stored).Property(nameof(ISharedState.IsShared)).CurrentValue = true;
            await dbContext.SaveChangesAsync();
        }

        public async Task<LocalNote> CopyTheNoteAsync(Guid originalLocalId, params string[] lines)
        {
            var copy = await Notes.CopyForEditingAsync(originalLocalId) ?? throw Refused();
            await Notes.UpdateAsync(
                copy.LocalId, new NoteContent(copy.Title, [.. lines.Select(NoteLine)], copy.Priority));

            return copy;
        }

        public Task<LocalTaskList> CopyTheTaskListAsync(Guid originalLocalId)
            => Copied(TaskLists.CopyForEditingAsync(originalLocalId));

        public Task<LocalCalendarEvent> CopyTheAppointmentAsync(Guid originalLocalId)
            => Copied(Appointments.CopyForEditingAsync(originalLocalId));

        public Task<LocalInventory> CopyTheInventoryAsync(Guid originalLocalId)
            => Copied(Inventories.CopyForEditingAsync(originalLocalId));

        public async Task WriteIntoTheTaskListAsync(Guid localId, params string[] descriptions)
        {
            var stored = await TaskLists.FindAsync(localId);
            await TaskLists.UpdateAsync(
                localId, new TaskListContent(stored!.Title, [.. descriptions.Select(Entry)], false, stored.Priority));
        }

        public async Task MoveTheAppointmentAsync(Guid localId, string startUtc)
        {
            var stored = await Appointments.FindAsync(localId);
            var moved = DateTimeOffset.Parse(startUtc);
            await Appointments.UpdateAsync(
                localId, stored!.Details with { StartUtc = moved, EndUtc = moved.AddHours(1) });
        }

        public async Task RestockAsync(Guid localId, decimal quantity)
        {
            var stored = await Inventories.FindAsync(localId);
            await Inventories.UpdateAsync(localId, new InventoryContent(stored!.Name, [Shelf(quantity)]));
        }

        /// <summary>Somebody else's change to the same note, as a pull would have brought it in.</summary>
        public async Task ChangeTheNoteAsync(Guid localId, params string[] lines)
        {
            await using var dbContext = _localStore.CreateDbContext();
            var note = dbContext.Notes.Single(candidate => candidate.LocalId == localId);
            note.Content = [.. lines.Select(NoteLine)];
            await dbContext.SaveChangesAsync();
        }

        /// <summary>What this phone has told itself, which is where a waiting copy announces itself.</summary>
        public IReadOnlyList<LocalNotification> Announcements()
        {
            using var dbContext = _localStore.CreateDbContext();
            return [.. dbContext.Notifications.Where(notice => notice.IsRaisedHere)];
        }

        /// <summary>What is still queued about one row - see the never-pushed test.</summary>
        public IReadOnlyList<string> QueuedFor(Guid localId)
        {
            using var dbContext = _localStore.CreateDbContext();
            return [.. dbContext.Outbox.Where(entry => entry.LocalId == localId)
                .Select(entry => entry.Operation.ToString())];
        }

        public async Task<CopyReviewViewModel> OpenAsync()
        {
            var screen = new CopyReviewViewModel(
                [Notes, TaskLists, Appointments, Inventories], _synchronizer,
                new Translations(new InMemoryLanguageStore()), new RecordingScreenNavigator());

            await screen.LoadCommand.ExecuteAsync(null);
            return screen;
        }

        private static async Task<TEntity> Copied<TEntity>(Task<TEntity?> copy) where TEntity : class
            => await copy ?? throw Refused();

        private static InvalidOperationException Refused() => new("The copy was refused.");

        private static NoteContentLineDto NoteLine(string text) => new(text, false, false);

        private static TaskItemDto Entry(string description)
            => new(Guid.NewGuid(), description, null, false, null, "None", false, "None", new TimeOnly(9, 0));

        private static InventoryItemRequest Shelf(decimal quantity)
            => new(Guid.NewGuid(), "Flour", "Dry", "Baking", quantity, 2, "Kilogram", null, "None");

        private static CalendarEventDetailsDto Details(string title)
            => new(
                title, null, null, null,
                DateTimeOffset.Parse("2026-09-01T09:00:00Z"), DateTimeOffset.Parse("2026-09-01T10:00:00Z"),
                false, null, [], [], ReminderNotificationChannel: "None");

        public void Dispose()
        {
            NoteServer.Dispose();
            _taskServer.Dispose();
            _calendarServer.Dispose();
            _inventoryServer.Dispose();
            _localStore.Dispose();
        }
    }
}

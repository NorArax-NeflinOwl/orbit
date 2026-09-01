using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Notifications;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.LinkCalendarEventToTaskList;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Putting an event on a task list, from the calendar's end. What matters is that the entry points at
/// the event rather than copying it - the two must not be able to come to disagree about when an
/// appointment is - and that adding one entry does not disturb the list around it.
/// </summary>
public sealed class LinkCalendarEventToTaskListCommandHandlerTests
{
    [Fact]
    public async Task The_list_gets_an_entry_pointing_at_the_event()
    {
        var context = new LinkingTestContext();
        var calendarEvent = await context.AnEventCalled("Dentist");
        var taskList = await context.AListCalled("Errands");

        var outcome = await context.LinkAsync(taskList.Id, calendarEvent.Id);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        var entry = Assert.Single((await context.ReadAsync(taskList.Id))!.Items);
        Assert.Equal(TaskItemKind.Calendar, entry.Kind);
        Assert.Equal(calendarEvent.Id, entry.LinkedCalendarEventId);
        // The title is what the row is read by; everything else stays on the event.
        Assert.Equal("Dentist", entry.Description);
    }

    /// <summary>
    /// The event says when it is. A due date here would put the same appointment on the calendar twice -
    /// once as itself, and once as something due.
    /// </summary>
    [Fact]
    public async Task The_entry_carries_no_date_of_its_own()
    {
        var context = new LinkingTestContext();
        var calendarEvent = await context.AnEventCalled("Dentist");
        var taskList = await context.AListCalled("Errands");

        await context.LinkAsync(taskList.Id, calendarEvent.Id);

        Assert.Null(Assert.Single((await context.ReadAsync(taskList.Id))!.Items).DueDateUtc);
    }

    [Fact]
    public async Task What_was_already_on_the_list_is_left_alone()
    {
        var context = new LinkingTestContext();
        var calendarEvent = await context.AnEventCalled("Dentist");
        var taskList = await context.AListCalled("Errands", TaskItem.Create("Buy milk", null, true));

        await context.LinkAsync(taskList.Id, calendarEvent.Id);

        var stored = await context.ReadAsync(taskList.Id);
        Assert.Equal(2, stored!.Items.Count);
        Assert.Equal("Buy milk", stored.Items[0].Description);
        Assert.True(stored.Items[0].IsCompleted);
        Assert.Equal(ItemPriority.High, stored.Priority);
    }

    [Fact]
    public async Task Adding_the_same_event_twice_leaves_one_entry()
    {
        var context = new LinkingTestContext();
        var calendarEvent = await context.AnEventCalled("Dentist");
        var taskList = await context.AListCalled("Errands");
        await context.LinkAsync(taskList.Id, calendarEvent.Id);

        // The list already says what it was asked to say, so this is the state the caller wanted.
        var outcome = await context.LinkAsync(taskList.Id, calendarEvent.Id);

        Assert.Equal(EditOutcomeKind.Success, outcome.Kind);
        Assert.Single((await context.ReadAsync(taskList.Id))!.Items);
    }

    [Fact]
    public async Task An_event_somebody_else_owns_is_not_found()
    {
        var context = new LinkingTestContext();
        var taskList = await context.AListCalled("Errands");
        var somebodyElsesEvent = await context.AnEventCalled("Their dentist", ownedBy: Guid.NewGuid());

        var outcome = await context.LinkAsync(taskList.Id, somebodyElsesEvent.Id);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
        Assert.Empty((await context.ReadAsync(taskList.Id))!.Items);
    }

    [Fact]
    public async Task A_list_somebody_else_owns_is_not_found()
    {
        var context = new LinkingTestContext();
        var calendarEvent = await context.AnEventCalled("Dentist");
        var somebodyElsesList = await context.AListCalled("Their errands", ownedBy: Guid.NewGuid());

        var outcome = await context.LinkAsync(somebodyElsesList.Id, calendarEvent.Id);

        Assert.Equal(EditOutcomeKind.NotFound, outcome.Kind);
    }

    /// <summary>
    /// A private list keeps no readable entries on the server - they live sealed inside it, where only
    /// its owner's browser can add one. The same rule moving an item between lists applies.
    /// </summary>
    [Fact]
    public async Task A_private_list_is_refused_rather_than_quietly_losing_the_entry()
    {
        var context = new LinkingTestContext();
        var calendarEvent = await context.AnEventCalled("Dentist");
        var privateList = await context.APrivateListAsync();

        await Assert.ThrowsAsync<InvalidRequestException>(
            () => context.LinkAsync(privateList.Id, calendarEvent.Id));
    }

    private sealed class LinkingTestContext
    {
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly InMemoryCalendarEventRepository _calendarEventRepository = new();

        public Guid UserId { get; } = Guid.NewGuid();

        public async Task<CalendarEvent> AnEventCalled(string title, Guid? ownedBy = null)
        {
            var start = DateTimeOffset.UtcNow.AddDays(1);
            var calendarEvent = CalendarEvent.Create(ownedBy ?? UserId, new CalendarEventDetails(
                title, Description: null, Location: null, Color: null, start, start.AddHours(1),
                IsAllDay: false, Recurrence: null, Guests: [], ReminderMinutesBeforeStart: [],
                ReminderNotificationChannel: NotificationChannel.None));
            await _calendarEventRepository.AddAsync(calendarEvent, CancellationToken.None);
            return calendarEvent;
        }

        /// <summary>High on purpose: adding an entry must not quietly reset what the list is worth.</summary>
        public async Task<TaskList> AListCalled(string title, TaskItem? item = null, Guid? ownedBy = null)
        {
            var taskList = TaskList.Create(
                ownedBy ?? UserId, title, item is null ? [] : [item], priority: ItemPriority.High);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public async Task<TaskList> APrivateListAsync()
        {
            var taskList = TaskList.Create(
                UserId, string.Empty, [], isPrivate: true,
                encryptedContent: new EncryptedPayload("c2VhbGVk", "bm9uY2U="));
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList;
        }

        public Task<EditOutcome> LinkAsync(Guid taskListId, Guid calendarEventId)
            => new LinkCalendarEventToTaskListCommandHandler(
                    new TaskListAccessResolver(_taskRepository, new InMemoryTaskListShareRepository(), new InMemoryUserRepository()),
                    new CalendarEventAccessResolver(_calendarEventRepository, new InMemoryCalendarEventShareRepository(), new InMemoryUserRepository()),
                    _taskRepository)
                .HandleAsync(new LinkCalendarEventToTaskListCommand(UserId, taskListId, calendarEventId), CancellationToken.None);

        public Task<TaskList?> ReadAsync(Guid taskListId)
            => _taskRepository.GetByIdAsync(UserId, taskListId, CancellationToken.None);
    }
}

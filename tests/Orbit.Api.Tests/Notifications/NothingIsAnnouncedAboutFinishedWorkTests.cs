using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Data;
using Orbit.Data.Entities;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// Work somebody has reported doing says nothing more about itself. Three background services ask a
/// repository what is owed - the overdue notices, the daily reminders and the calendar's own reminders -
/// and each of them used to answer with things that were already done: a list its owner had closed with
/// an overdue entry still on it went on saying so every morning, and an appointment a list raised went
/// on reminding after its entry had been ticked off.
///
/// The real repositories rather than the in-memory doubles, because the rule is in the query: a double
/// hands back whatever it was seeded with, so a filter that was never written passes there. SQLite
/// stands in for PostgreSQL as it does in FolderRepositoryTests - what is being tested is which rows
/// the query decides to return, not anything provider-specific.
/// </summary>
public sealed class NothingIsAnnouncedAboutFinishedWorkTests : IDisposable
{
    private readonly TemporarySqliteDatabase _database = new();
    private readonly OrbitDbContext _dbContext;
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly DateTimeOffset Yesterday = DateTimeOffset.UtcNow.AddDays(-1);

    public NothingIsAnnouncedAboutFinishedWorkTests() => _dbContext = _database.DbContext;

    public void Dispose() => _database.Dispose();

    /// <summary>
    /// The real repositories, each with the live completion an entry pointing at other lists is read by -
    /// see LinkedEntryCompletion, which walks the owner's own lists through the resolver every read of a
    /// list already uses. An ordinary entry never reaches it, so these tests are unaffected by it.
    /// </summary>
    private OverdueTaskNotificationRepository OverdueNotices => new(_dbContext, LinkedEntries);

    /// <inheritdoc cref="OverdueNotices"/>
    private DailyTaskReminderRepository DailyReminders => new(_dbContext, LinkedEntries);

    private LinkedEntryCompletion LinkedEntries
        => new(new TaskRepository(_dbContext), new LinkedTaskCompletionResolver());

    [Fact]
    public async Task An_overdue_entry_on_an_open_list_is_still_announced()
    {
        AListWithAnOverdueEntry(isListDone: false);
        await _dbContext.SaveChangesAsync();

        var overdue = await OverdueNotices.GetIncompleteWithDueDateAsync(CancellationToken.None);

        Assert.Single(overdue);
    }

    /// <summary>
    /// Saying "no more of this" and then being told about it every morning is the app arguing with a
    /// decision the reader made - see TaskList.IsCompleted, either way of reaching it.
    /// </summary>
    [Fact]
    public async Task An_overdue_entry_on_a_finished_list_is_not()
    {
        AListWithAnOverdueEntry(isListDone: true);
        await _dbContext.SaveChangesAsync();

        var overdue = await OverdueNotices.GetIncompleteWithDueDateAsync(CancellationToken.None);

        Assert.Empty(overdue);
    }

    [Fact]
    public async Task A_daily_reminder_on_an_open_list_is_still_sent()
    {
        AListWithADailyReminder(isListDone: false);
        await _dbContext.SaveChangesAsync();

        var due = await DailyReminders.GetEligibleAsync(CancellationToken.None);

        Assert.Single(due);
    }

    /// <summary>The list's own tick closes it, whichever way it was reached - see TaskList.IsCompleted.</summary>
    [Fact]
    public async Task A_daily_reminder_on_a_finished_list_is_not()
    {
        AListWithADailyReminder(isListDone: true);
        await _dbContext.SaveChangesAsync();

        var due = await DailyReminders.GetEligibleAsync(CancellationToken.None);

        Assert.Empty(due);
    }

    /// <summary>
    /// "Remind daily" asks about one errand until it is done - it is not a claim that the errand happens
    /// every day. So the tick ends the asking, and the reader's own list said so: a doctor's appointment
    /// booked on Tuesday came back unticked on Wednesday morning with a reminder that it was still
    /// waiting (2026-09-16). The shelf's standing round is the one thing that does come back, below.
    /// </summary>
    [Fact]
    public async Task A_daily_reminder_stops_once_the_entry_is_ticked_off()
    {
        AListWithADailyReminder(isListDone: false, isEntryDone: true);
        await _dbContext.SaveChangesAsync();

        var due = await DailyReminders.GetEligibleAsync(CancellationToken.None);

        Assert.Empty(due);
    }

    /// <summary>A cross is the other way of being finished with something, and ends the asking the same way.</summary>
    [Fact]
    public async Task A_daily_reminder_stops_once_the_entry_is_crossed_out()
    {
        AListWithADailyReminder(isListDone: false, isEntryFailed: true);
        await _dbContext.SaveChangesAsync();

        var due = await DailyReminders.GetEligibleAsync(CancellationToken.None);

        Assert.Empty(due);
    }

    /// <summary>
    /// The exception, and the reason the whole mechanism exists: a shelf's standing round is work that
    /// happens again tomorrow whatever was done about it today, so it comes back ticked and is the one
    /// candidate marked to be reopened.
    /// </summary>
    [Fact]
    public async Task The_shelfs_standing_round_comes_back_after_it_is_ticked_off()
    {
        var taskListId = AShelfsRestockList(isEntryDone: true);
        await _dbContext.SaveChangesAsync();

        var due = await DailyReminders.GetEligibleAsync(CancellationToken.None);

        var standing = Assert.Single(due);
        Assert.Equal(taskListId, standing.TaskListId);
        Assert.True(standing.ComesRoundAgain);
    }

    /// <summary>
    /// And it is the shelf's list that makes it one, not the words: an entry somebody happens to name
    /// the same thing on a list of their own is their errand and stops when they tick it off.
    /// </summary>
    [Fact]
    public async Task The_same_words_on_somebodys_own_list_are_still_their_errand()
    {
        AList(isListDone: false, new TaskItemEntity
        {
            Id = Guid.NewGuid(),
            Description = "Update stock levels",
            IsCompleted = true,
            RemindDaily = true,
            DailyReminderNotificationChannel = "Push",
            DailyReminderTimeOfDayMinutes = 9 * 60
        });
        await _dbContext.SaveChangesAsync();

        var due = await DailyReminders.GetEligibleAsync(CancellationToken.None);

        Assert.Empty(due);
    }

    /// <summary>An ordinary errand is not marked to come back, so nothing ever un-ticks it.</summary>
    [Fact]
    public async Task An_ordinary_errand_is_not_one_that_comes_round_again()
    {
        AListWithADailyReminder(isListDone: false);
        await _dbContext.SaveChangesAsync();

        var due = await DailyReminders.GetEligibleAsync(CancellationToken.None);

        Assert.False(Assert.Single(due).ComesRoundAgain);
    }

    [Fact]
    public async Task An_appointment_of_its_own_still_reminds()
    {
        AnEventWithAReminder();
        await _dbContext.SaveChangesAsync();

        var events = await new EventReminderRepository(_dbContext)
            .GetAllWithRemindersConfiguredAsync(CancellationToken.None);

        Assert.Single(events);
    }

    /// <summary>
    /// An appointment a list raised is done when its entry is, whatever the clock says: the shopping was
    /// done on Tuesday for a slot booked on Friday, and reminding about Friday is reminding somebody of
    /// work they have already reported doing.
    /// </summary>
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task An_appointment_a_finished_entry_or_list_raised_does_not(bool isEntryDone, bool isListDone)
    {
        var calendarEventId = AnEventWithAReminder();
        AListWhoseEntryRaised(calendarEventId, isEntryDone, isListDone);
        await _dbContext.SaveChangesAsync();

        var events = await new EventReminderRepository(_dbContext)
            .GetAllWithRemindersConfiguredAsync(CancellationToken.None);

        Assert.Empty(events);
    }

    [Fact]
    public async Task An_appointment_an_open_entry_raised_still_reminds()
    {
        var calendarEventId = AnEventWithAReminder();
        AListWhoseEntryRaised(calendarEventId, isEntryDone: false, isListDone: false);
        await _dbContext.SaveChangesAsync();

        var events = await new EventReminderRepository(_dbContext)
            .GetAllWithRemindersConfiguredAsync(CancellationToken.None);

        Assert.Single(events);
    }

    private void AListWithAnOverdueEntry(bool isListDone)
        => AList(isListDone, new TaskItemEntity
        {
            Id = Guid.NewGuid(),
            Description = "Pay the deposit",
            DueDateUtc = Yesterday,
            OverdueNotificationChannel = "Push"
        });

    private void AListWithADailyReminder(bool isListDone, bool isEntryDone = false, bool isEntryFailed = false)
        => AList(isListDone, new TaskItemEntity
        {
            Id = Guid.NewGuid(),
            Description = "Water the plants",
            IsCompleted = isEntryDone,
            IsFailed = isEntryFailed,
            RemindDaily = true,
            DailyReminderNotificationChannel = "Push",
            DailyReminderTimeOfDayMinutes = 9 * 60
        });

    /// <summary>
    /// A list an inventory keeps for itself, carrying the standing round - the shape
    /// InventoryTaskListCoordinator builds, down to the words the entry is written with.
    /// </summary>
    private Guid AShelfsRestockList(bool isEntryDone)
    {
        var taskListId = Guid.NewGuid();
        _dbContext.Tasks.Add(new TaskEntity
        {
            Id = taskListId,
            UserId = OwnerUserId,
            Title = "Restock supplies - Pantry",
            CreatedAtUtc = Yesterday,
            UpdatedAtUtc = Yesterday,
            Items =
            [
                new TaskItemEntity
                {
                    Id = Guid.NewGuid(),
                    Description = "Update stock levels",
                    IsCompleted = isEntryDone,
                    RemindDaily = true,
                    DailyReminderNotificationChannel = "Push",
                    DailyReminderTimeOfDayMinutes = 9 * 60
                }
            ]
        });

        _dbContext.InventoryManagedTaskLists.Add(new InventoryManagedTaskListEntity
        {
            Id = Guid.NewGuid(),
            InventoryId = Guid.NewGuid(),
            TaskListId = taskListId
        });

        return taskListId;
    }

    private void AListWhoseEntryRaised(Guid calendarEventId, bool isEntryDone, bool isListDone)
        => AList(isListDone, new TaskItemEntity
        {
            Id = Guid.NewGuid(),
            Description = "Pick up the keys",
            IsCompleted = isEntryDone,
            LinkedCalendarEventId = calendarEventId,
            Kind = nameof(Orbit.Core.Tasks.TaskItemKind.Calendar)
        });

    private void AList(bool isListDone, TaskItemEntity entry)
    {
        _dbContext.Tasks.Add(new TaskEntity
        {
            Id = Guid.NewGuid(),
            UserId = OwnerUserId,
            Title = "Moving",
            IsCompleted = isListDone,
            CreatedAtUtc = Yesterday,
            UpdatedAtUtc = Yesterday,
            Items = [entry]
        });
    }

    private Guid AnEventWithAReminder()
    {
        var calendarEventId = Guid.NewGuid();
        _dbContext.CalendarEvents.Add(new CalendarEventEntity
        {
            Id = calendarEventId,
            UserId = OwnerUserId,
            Title = "Pick up the keys",
            StartUtc = DateTimeOffset.UtcNow.AddDays(3),
            EndUtc = DateTimeOffset.UtcNow.AddDays(3).AddHours(1),
            NotifyAtStart = true,
            ReminderNotificationChannel = "Push",
            CreatedAtUtc = Yesterday,
            UpdatedAtUtc = Yesterday
        });
        return calendarEventId;
    }
}

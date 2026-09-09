using Orbit.Api.Tests.TestDoubles;
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

    [Fact]
    public async Task An_overdue_entry_on_an_open_list_is_still_announced()
    {
        AListWithAnOverdueEntry(isListDone: false);
        await _dbContext.SaveChangesAsync();

        var overdue = await new OverdueTaskNotificationRepository(_dbContext)
            .GetIncompleteWithDueDateAsync(CancellationToken.None);

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

        var overdue = await new OverdueTaskNotificationRepository(_dbContext)
            .GetIncompleteWithDueDateAsync(CancellationToken.None);

        Assert.Empty(overdue);
    }

    [Fact]
    public async Task A_daily_reminder_on_an_open_list_is_still_sent()
    {
        AListWithADailyReminder(isListDone: false);
        await _dbContext.SaveChangesAsync();

        var due = await new DailyTaskReminderRepository(_dbContext).GetEligibleAsync(CancellationToken.None);

        Assert.Single(due);
    }

    /// <summary>
    /// The entry's own tick is deliberately ignored here - a daily errand is done today and due again
    /// tomorrow - but the list's is not the same question, and a closed list is not owed anything.
    /// </summary>
    [Fact]
    public async Task A_daily_reminder_on_a_finished_list_is_not()
    {
        AListWithADailyReminder(isListDone: true);
        await _dbContext.SaveChangesAsync();

        var due = await new DailyTaskReminderRepository(_dbContext).GetEligibleAsync(CancellationToken.None);

        Assert.Empty(due);
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

    private void AListWithADailyReminder(bool isListDone)
        => AList(isListDone, new TaskItemEntity
        {
            Id = Guid.NewGuid(),
            Description = "Water the plants",
            RemindDaily = true,
            DailyReminderNotificationChannel = "Push",
            DailyReminderTimeOfDayMinutes = 9 * 60
        });

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

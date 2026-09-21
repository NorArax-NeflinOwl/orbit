using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Data;
using Orbit.Data.Entities;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Notifications;

/// <summary>
/// An entry that stands for other lists - or is done one of several ways, one of them a list - has a
/// deadline like any other, and used to say nothing when it passed. Both reminder queries left every
/// such entry out, because its stored tick is always "not done" (see TaskItem.Create) and a query cannot
/// tell that from work still owed. Reported by the user on 2026-09-21: an entry due at 17:00 that stood
/// for another list never spoke, while an ordinary entry due at the same minute did.
///
/// Now they are kept and the lists behind them are asked, through the resolver every read of a list
/// already uses - see LinkedEntryCompletion. The real repositories rather than the in-memory doubles,
/// for the reason NothingIsAnnouncedAboutFinishedWorkTests gives: the rule lives in what the query and
/// its follow-up decide to return, and a double hands back whatever it was seeded with.
/// </summary>
public sealed class AnEntryStandingForListsStillSpeaksTests : IDisposable
{
    private readonly TemporarySqliteDatabase _database = new();
    private readonly OrbitDbContext _dbContext;
    private static readonly Guid OwnerUserId = Guid.NewGuid();
    private static readonly DateTimeOffset Yesterday = DateTimeOffset.UtcNow.AddDays(-1);

    public AnEntryStandingForListsStillSpeaksTests() => _dbContext = _database.DbContext;

    public void Dispose() => _database.Dispose();

    [Fact]
    public async Task An_overdue_entry_standing_for_unfinished_work_is_announced()
    {
        AnEntryStandingFor(AListOfItsOwn(isDone: false), AnOverdueEntry());
        await _dbContext.SaveChangesAsync();

        var overdue = await OverdueNotices.GetIncompleteWithDueDateAsync(CancellationToken.None);

        Assert.Single(overdue);
    }

    /// <summary>
    /// And the other half of the same rule: the list it stands for is what finishes it, so once that
    /// list is done the deadline is nobody's business any more.
    /// </summary>
    [Fact]
    public async Task An_overdue_entry_whose_list_is_finished_is_not()
    {
        AnEntryStandingFor(AListOfItsOwn(isDone: true), AnOverdueEntry());
        await _dbContext.SaveChangesAsync();

        var overdue = await OverdueNotices.GetIncompleteWithDueDateAsync(CancellationToken.None);

        Assert.Empty(overdue);
    }

    [Fact]
    public async Task A_daily_reminder_on_an_entry_standing_for_unfinished_work_is_sent()
    {
        AnEntryStandingFor(AListOfItsOwn(isDone: false), AnEntryRemindedDaily());
        await _dbContext.SaveChangesAsync();

        var due = await DailyReminders.GetEligibleAsync(CancellationToken.None);

        Assert.Single(due);
    }

    [Fact]
    public async Task A_daily_reminder_on_an_entry_whose_list_is_finished_is_not()
    {
        AnEntryStandingFor(AListOfItsOwn(isDone: true), AnEntryRemindedDaily());
        await _dbContext.SaveChangesAsync();

        var due = await DailyReminders.GetEligibleAsync(CancellationToken.None);

        Assert.Empty(due);
    }

    /// <summary>
    /// A way that is a list is read the same way, and any one way finishes the entry - see
    /// TaskItem.Alternatives.
    /// </summary>
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task An_entry_done_one_of_several_ways_follows_the_list_among_them(bool isThatListDone, int announced)
    {
        var entry = AnOverdueEntry();
        entry.Alternatives =
        [
            new TaskItemAlternativeEntity { TaskItemId = entry.Id, Position = 0, Description = "Buy one" },
            new TaskItemAlternativeEntity
            {
                TaskItemId = entry.Id,
                Position = 1,
                Description = "Make one",
                LinkedTaskListId = AListOfItsOwn(isThatListDone)
            }
        ];
        AList(entry);
        await _dbContext.SaveChangesAsync();

        var overdue = await OverdueNotices.GetIncompleteWithDueDateAsync(CancellationToken.None);

        Assert.Equal(announced, overdue.Count);
    }

    /// <summary>
    /// The real repository, with the live completion an entry pointing at other lists is read by - see
    /// LinkedEntryCompletion, which is the half of the answer the query itself cannot give.
    /// </summary>
    private OverdueTaskNotificationRepository OverdueNotices => new(_dbContext, LinkedEntries);

    /// <inheritdoc cref="OverdueNotices"/>
    private DailyTaskReminderRepository DailyReminders => new(_dbContext, LinkedEntries);

    private LinkedEntryCompletion LinkedEntries
        => new(new TaskRepository(_dbContext), new LinkedTaskCompletionResolver());

    private static TaskItemEntity AnOverdueEntry()
        => new()
        {
            Id = Guid.NewGuid(),
            Description = "Medicines",
            DueDateUtc = Yesterday,
            OverdueNotificationChannel = "Push"
        };

    private static TaskItemEntity AnEntryRemindedDaily()
        => new()
        {
            Id = Guid.NewGuid(),
            Description = "Medicines",
            RemindDaily = true,
            DailyReminderNotificationChannel = "Push",
            DailyReminderTimeOfDayMinutes = 9 * 60
        };

    /// <summary>The list an entry points at, with one entry of its own saying whether it is finished.</summary>
    private Guid AListOfItsOwn(bool isDone)
    {
        var taskListId = Guid.NewGuid();
        _dbContext.Tasks.Add(ATaskList(taskListId, "Medicines", new TaskItemEntity
        {
            Id = Guid.NewGuid(),
            Description = "Collect the prescription",
            IsCompleted = isDone
        }));

        return taskListId;
    }

    private void AnEntryStandingFor(Guid linkedTaskListId, TaskItemEntity entry)
    {
        entry.LinkedTaskLists =
        [
            new TaskItemTaskListLinkEntity { TaskItemId = entry.Id, LinkedTaskListId = linkedTaskListId, Position = 0 }
        ];
        AList(entry);
    }

    private void AList(TaskItemEntity entry) => _dbContext.Tasks.Add(ATaskList(Guid.NewGuid(), "Panda", entry));

    private static TaskEntity ATaskList(Guid id, string title, TaskItemEntity entry)
        => new()
        {
            Id = id,
            UserId = OwnerUserId,
            Title = title,
            CreatedAtUtc = Yesterday,
            UpdatedAtUtc = Yesterday,
            Items = [entry]
        };
}

using Orbit.Core.Notifications;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Covers where a task list says it has got to. Derived rather than stored, so these are really about
/// the reading being right for each shape of list rather than about anything being saved.
/// </summary>
public sealed class TaskListStatusTests
{
    [Fact]
    public void An_empty_list_has_not_been_started()
    {
        // Nothing to tick, so nothing has been ticked - and calling it Completed would be worse, since
        // an empty list is the one thing nobody has done any of.
        Assert.Equal(TaskListStatus.New, ListWith().Status);
    }

    [Fact]
    public void A_list_with_nothing_ticked_has_not_been_started()
        => Assert.Equal(TaskListStatus.New, ListWith(Item("Buy milk"), Item("Buy bread")).Status);

    [Fact]
    public void A_partly_ticked_list_is_in_progress()
        => Assert.Equal(TaskListStatus.Pending, ListWith(Item("Buy milk", isCompleted: true), Item("Buy bread")).Status);

    [Fact]
    public void A_fully_ticked_list_is_done()
        => Assert.Equal(TaskListStatus.Completed, ListWith(Item("Buy milk", isCompleted: true)).Status);

    [Fact]
    public void An_item_past_its_due_date_makes_the_list_overdue()
        => Assert.Equal(TaskListStatus.Overdue, ListWith(Item("File the return", dueDaysAgo: 1)).Status);

    [Fact]
    public void Being_overdue_outranks_being_in_progress()
    {
        var taskList = ListWith(Item("Buy milk", isCompleted: true), Item("File the return", dueDaysAgo: 1));

        // A list that is late is late whether or not someone has started on it.
        Assert.Equal(TaskListStatus.Overdue, taskList.Status);
    }

    [Fact]
    public void A_finished_list_is_never_overdue()
    {
        var taskList = ListWith(Item("File the return", isCompleted: true, dueDaysAgo: 5));

        // Nothing is left to be late for.
        Assert.Equal(TaskListStatus.Completed, taskList.Status);
    }

    [Fact]
    public void A_due_date_still_ahead_is_not_overdue()
        => Assert.Equal(TaskListStatus.New, ListWith(Item("File the return", dueDaysAgo: -3)).Status);

    /// <summary>
    /// A chore that comes round every day keeps one due date that never moves, so the moment it passes
    /// the list would read "late" for as long as the chore exists - and it is not late, it is due again.
    /// A restock round is the one every account has, so this was the first thing a shelf did to a page.
    /// </summary>
    [Fact]
    public void A_daily_chore_left_undone_makes_the_list_due_again_rather_than_overdue()
        => Assert.Equal(
            TaskListStatus.DueAgain,
            ListWith(Item("Update stock levels", dueDaysAgo: 1, remindDaily: true)).Status);

    /// <summary>
    /// A missed deadline still wins where a list carries both. Being told about the chore instead would
    /// be the page choosing the smaller of two things to say.
    /// </summary>
    [Fact]
    public void A_missed_deadline_outranks_a_daily_chore()
        => Assert.Equal(
            TaskListStatus.Overdue,
            ListWith(
                Item("Update stock levels", dueDaysAgo: 1, remindDaily: true),
                Item("File the return", dueDaysAgo: 3)).Status);

    /// <summary>And a daily chore whose date is still ahead says nothing at all, like any other entry.</summary>
    [Fact]
    public void A_daily_chore_still_ahead_leaves_the_list_alone()
        => Assert.Equal(
            TaskListStatus.New,
            ListWith(Item("Update stock levels", dueDaysAgo: -1, remindDaily: true)).Status);

    /// <summary>Done for today is done: the reminder reopens it tomorrow, and until then nothing is owed.</summary>
    [Fact]
    public void A_daily_chore_already_done_leaves_the_list_finished()
        => Assert.Equal(
            TaskListStatus.Completed,
            ListWith(Item("Update stock levels", isCompleted: true, dueDaysAgo: 1, remindDaily: true)).Status);

    [Fact]
    public void A_private_list_reads_as_not_started()
    {
        var taskList = TaskList.Create(
            Guid.NewGuid(), string.Empty, [], isPrivate: true,
            encryptedContent: new Orbit.Core.Abstractions.EncryptedPayload("c2VhbGVk", "bm9uY2U="));

        // Its items are sealed, so there is nothing here to work a status out from - and guessing would
        // be worse than saying nothing.
        Assert.Equal(TaskListStatus.New, taskList.Status);
    }

    [Fact]
    public void A_list_defaults_to_normal_priority()
        => Assert.Equal(ItemPriority.Normal, ListWith().Priority);

    [Fact]
    public void Updating_a_list_keeps_the_priority_it_was_given()
    {
        var taskList = TaskList.Create(Guid.NewGuid(), "Errands", [], priority: ItemPriority.High);

        taskList.Update("Errands", [Item("Buy milk")], isGroup: false, isPrivate: false, encryptedContent: null,
            priority: ItemPriority.High);

        Assert.Equal(ItemPriority.High, taskList.Priority);
    }

    [Fact]
    public void Rebuilding_a_list_in_memory_keeps_its_priority()
    {
        // LinkedTaskCompletionResolver rebuilds every list through FromPersistence to resolve linked
        // items, and dropped the priority silently while that parameter was optional - which is why it
        // no longer is.
        var original = TaskList.Create(Guid.NewGuid(), "Errands", [Item("Buy milk")], priority: ItemPriority.High);

        var rebuilt = TaskList.FromPersistence(
            original.Id, original.UserId, original.Title, original.Items, original.IsGroup, original.IsPrivate,
            original.EncryptedContent, original.CreatedAtUtc, original.UpdatedAtUtc,
            original.LockedByUserId, original.LockedByUserName, original.LockExpiresAtUtc, original.Priority, original.IsPinned);

        Assert.Equal(ItemPriority.High, rebuilt.Priority);
    }

    private static TaskList ListWith(params TaskItem[] items) => TaskList.Create(Guid.NewGuid(), "Errands", items);

    private static TaskItem Item(
        string description, bool isCompleted = false, int? dueDaysAgo = null, bool remindDaily = false)
        => TaskItem.Create(
            description,
            dueDaysAgo is null ? null : DateTimeOffset.UtcNow.AddDays(-dueDaysAgo.Value),
            isCompleted,
            linkedTaskListIds: null,
            new TaskItemReminders(NotificationChannel.None, remindDaily, NotificationChannel.None, new TimeOnly(9, 0)));
}

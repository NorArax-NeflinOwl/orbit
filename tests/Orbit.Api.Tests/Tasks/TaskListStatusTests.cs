using Orbit.Core.Notifications;
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
        => Assert.Equal(TaskListPriority.Normal, ListWith().Priority);

    [Fact]
    public void Updating_a_list_keeps_the_priority_it_was_given()
    {
        var taskList = TaskList.Create(Guid.NewGuid(), "Errands", [], priority: TaskListPriority.High);

        taskList.Update("Errands", [Item("Buy milk")], isGroup: false, isPrivate: false, encryptedContent: null,
            priority: TaskListPriority.High);

        Assert.Equal(TaskListPriority.High, taskList.Priority);
    }

    [Fact]
    public void Rebuilding_a_list_in_memory_keeps_its_priority()
    {
        // LinkedTaskCompletionResolver rebuilds every list through FromPersistence to resolve linked
        // items, and dropped the priority silently while that parameter was optional - which is why it
        // no longer is.
        var original = TaskList.Create(Guid.NewGuid(), "Errands", [Item("Buy milk")], priority: TaskListPriority.High);

        var rebuilt = TaskList.FromPersistence(
            original.Id, original.UserId, original.Title, original.Items, original.IsGroup, original.IsPrivate,
            original.EncryptedContent, original.CreatedAtUtc, original.UpdatedAtUtc,
            original.LockedByUserId, original.LockedByUserName, original.LockExpiresAtUtc, original.Priority, original.IsPinned);

        Assert.Equal(TaskListPriority.High, rebuilt.Priority);
    }

    private static TaskList ListWith(params TaskItem[] items) => TaskList.Create(Guid.NewGuid(), "Errands", items);

    private static TaskItem Item(string description, bool isCompleted = false, int? dueDaysAgo = null)
        => TaskItem.Create(
            description,
            dueDaysAgo is null ? null : DateTimeOffset.UtcNow.AddDays(-dueDaysAgo.Value),
            isCompleted,
            linkedTaskListId: null,
            NotificationChannel.None,
            remindDaily: false,
            NotificationChannel.None,
            new TimeOnly(9, 0));
}

using System.Reflection;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// The resolver rebuilds every task list through FromPersistence, and it sits on the path of every read.
/// A field left out of that rebuild is stored correctly, works in the handler that reads the row, and
/// arrives at the client as null - which is a hard bug to trace back here, and has already happened
/// twice.
///
/// So rather than trusting a reviewer to notice, this walks every property a task list has and checks
/// each one survives the trip.
/// </summary>
public sealed class LinkedTaskCompletionResolverRebuildTests
{
    /// <summary>
    /// Rebuilt from the lists themselves rather than carried across, so they are checked separately -
    /// see the assertions below.
    /// </summary>
    private static readonly string[] RebuiltRatherThanCarried = [nameof(TaskList.Items), nameof(TaskList.IsCompleted)];

    [Fact]
    public void Every_field_a_task_list_has_survives_the_rebuild()
    {
        var original = ATaskListWithEveryFieldSet();

        var resolved = Assert.Single(new LinkedTaskCompletionResolver().ResolveAll([original]));

        var unchecked_ = new List<string>();
        foreach (var property in typeof(TaskList).GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (RebuiltRatherThanCarried.Contains(property.Name))
            {
                continue;
            }

            var before = property.GetValue(original);
            var after = property.GetValue(resolved);
            Assert.True(
                Equals(before, after),
                $"TaskList.{property.Name} was {before ?? "null"} and came back {after ?? "null"} - " +
                "LinkedTaskCompletionResolver rebuilds the list and this field was left out of that rebuild.");
            unchecked_.Add(property.Name);
        }

        // A guard on the guard: if TaskList ever loses its properties to a different shape, this test
        // would pass by checking nothing at all.
        Assert.True(unchecked_.Count >= 15, $"Only {unchecked_.Count} properties were checked - is this still walking TaskList?");
    }

    [Fact]
    public void The_items_come_through_as_the_same_entries()
    {
        var original = ATaskListWithEveryFieldSet();

        var resolved = Assert.Single(new LinkedTaskCompletionResolver().ResolveAll([original]));

        Assert.Equal(
            original.Items.Select(item => (item.Id, item.Description, item.DueDateUtc)),
            resolved.Items.Select(item => (item.Id, item.Description, item.DueDateUtc)));
    }

    private static TaskList ATaskListWithEveryFieldSet()
    {
        var taskList = TaskList.FromPersistence(
            Guid.NewGuid(), Guid.NewGuid(), "Everything",
            [TaskItem.Create("A thing to do", new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero), isCompleted: false)],
            isGroup: true, isPrivate: false, new EncryptedPayload("c2VhbGVk", "bm9uY2U="),
            createdAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            updatedAtUtc: new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero),
            lockedByUserId: Guid.NewGuid(), lockedByUserName: "someone",
            lockExpiresAtUtc: new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero),
            ItemPriority.High, isPinned: true, linkedInventoryId: Guid.NewGuid(),
            description: "What this list is for");
        taskList.SetAccessContext(isShared: true, sharedByUserName: "anna", ShareAccessLevel.ReadOnly);
        // Every field set to something other than its default, or the walk below compares two defaults
        // and passes on a field the rebuild drops - which is how Description and IsSharedWithOthers both
        // went missing while this test was watching.
        taskList.SetSharedWithOthers(true);
        return taskList;
    }
}

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

    /// <summary>
    /// The fixture has to set every field to something other than its default, and this is what makes
    /// sure it does. Without it the walk above compares two nulls and passes on a field the rebuild
    /// drops - which is not hypothetical: it is how Description and IsSharedWithOthers went missing, and
    /// then FolderId, which was left out of the fixture and so left out of the rebuild for as long as
    /// folders existed. Every list came back filed nowhere, and it was reported as a Save that did not
    /// save.
    ///
    /// A new field on TaskList therefore fails *this* test until somebody puts it in the fixture, which
    /// is the point: the walk above cannot check what the fixture never set.
    /// </summary>
    [Fact]
    public void The_fixture_sets_every_field_to_something_it_would_not_have_by_default()
    {
        var original = ATaskListWithEveryFieldSet();

        // Against a list built with nothing set rather than against default(T): the value a field has
        // when nobody chose one is the property's own business - AccessLevel starts at CanEdit, not at
        // whichever member happens to be zero - and this is the only comparison that knows that.
        var bare = ABareTaskList();
        var leftAtItsDefault = typeof(TaskList).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(property => !MayBeLeftAtItsDefault.Contains(property.Name))
            .Where(property => Equals(property.GetValue(original), property.GetValue(bare)))
            .Select(property => property.Name)
            .ToList();

        Assert.True(leftAtItsDefault.Count == 0,
            "These are still at their default in ATaskListWithEveryFieldSet, so the walk above compares "
                + $"two defaults and would pass on a rebuild that dropped them: {string.Join(", ", leftAtItsDefault)}");
    }

    /// <summary>
    /// The two that cannot be set here, listed rather than inferred so each stays a decision. A private
    /// list has to arrive sealed, with its title and items empty, which is a different list from the one
    /// these tests need - and a list that is not private has its payload dropped on the way in
    /// (TaskList.ReadableOrSealed), so EncryptedContent cannot be non-null while IsPrivate is false.
    /// </summary>
    private static readonly string[] MayBeLeftAtItsDefault =
        [nameof(TaskList.IsPrivate), nameof(TaskList.EncryptedContent)];

    /// <summary>A list with nothing chosen, to compare the fixture against - see the test that uses it.</summary>
    private static TaskList ABareTaskList()
        => TaskList.FromPersistence(
            Guid.Empty, Guid.Empty, string.Empty, [], isGroup: false, isPrivate: false, encryptedContent: null,
            createdAtUtc: default, updatedAtUtc: default,
            lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null,
            ItemPriority.Normal, isPinned: false);

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
            [TaskItem.Create("A thing to do", new DateTimeOffset(2026, 9, 1, 10, 0, 0, TimeSpan.Zero), isCompleted: true)],
            isGroup: true, isPrivate: false, new EncryptedPayload("c2VhbGVk", "bm9uY2U="),
            createdAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            updatedAtUtc: new DateTimeOffset(2026, 2, 2, 0, 0, 0, TimeSpan.Zero),
            lockedByUserId: Guid.NewGuid(), lockedByUserName: "someone",
            lockExpiresAtUtc: new DateTimeOffset(2026, 3, 3, 0, 0, 0, TimeSpan.Zero),
            ItemPriority.High, isPinned: true, linkedInventoryId: Guid.NewGuid(),
            description: "What this list is for", folderId: Guid.NewGuid());
        taskList.SetAccessContext(isShared: true, sharedByUserName: "anna", ShareAccessLevel.ReadOnly);
        // Every field set to something other than its default, or the walk below compares two defaults
        // and passes on a field the rebuild drops - which is how Description and IsSharedWithOthers both
        // went missing while this test was watching.
        taskList.SetSharedWithOthers(true);
        return taskList;
    }
}

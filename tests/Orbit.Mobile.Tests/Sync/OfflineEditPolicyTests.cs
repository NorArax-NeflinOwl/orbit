using Orbit.Contracts.Tasks;
using Orbit.Mobile.Data;
using Orbit.Mobile.Tests.TestDoubles;
using Orbit.Mobile.Sync;
using Xunit;

namespace Orbit.Mobile.Tests.Sync;

/// <summary>
/// The restrictive policy from info/orbit-maui-plan.md §5.4. The decision it encodes is not obvious, so
/// it is worth restating: sharing in Orbit is not a copy - two people with CanEdit are editing one row,
/// which is why the server holds edit locks. A phone cannot hold a lock, so it can only find out at
/// replay time that someone else was editing, long after the user did the work. Refusing up front is
/// the honest option.
/// </summary>
public sealed class OfflineEditPolicyTests
{
    [Fact]
    public void Offline_a_note_nobody_else_can_touch_is_editable()
    {
        var refusal = OfflineEditPolicy.Evaluate(new LocalNote(), Offline);

        Assert.Equal(OfflineEditRefusal.None, refusal);
    }

    [Fact]
    public void Offline_a_note_somebody_shared_with_you_is_not_editable()
    {
        var refusal = OfflineEditPolicy.Evaluate(new LocalNote { IsShared = true }, Offline);

        Assert.Equal(OfflineEditRefusal.SharedWithYou, refusal);
    }

    [Fact]
    public void Offline_a_note_you_shared_out_is_not_editable_either()
    {
        // The owner's side, and the case the API could not answer until IsSharedWithOthers existed -
        // without it this note is indistinguishable from a private one.
        var refusal = OfflineEditPolicy.Evaluate(new LocalNote { IsSharedWithOthers = true }, Offline);

        Assert.Equal(OfflineEditRefusal.SharedWithOthers, refusal);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public void Online_the_server_decides_and_this_policy_refuses_nothing(bool isShared, bool isSharedWithOthers)
    {
        var note = new LocalNote { IsShared = isShared, IsSharedWithOthers = isSharedWithOthers };

        // Online the app can take a real edit lock, which is a better answer than guessing.
        Assert.True(OfflineEditPolicy.IsAllowed(note, Online));
    }

    private static INetworkStatus Offline => Orbit.Mobile.Tests.TestDoubles.FixedNetworkStatus.Offline;

    private static INetworkStatus Online => Orbit.Mobile.Tests.TestDoubles.FixedNetworkStatus.Online;
}

/// <summary>
/// The policy is only worth anything if it is enforced where writes happen. Displaying it on a screen
/// leaves every future screen free to forget - and the outbox is where forgetting would do the damage,
/// because a queued edit to a shared item is exactly what the policy exists to prevent.
/// </summary>
public sealed class OfflineEditEnforcementTests
{
    private static readonly IReadOnlyList<TaskItemDto> SomeItems =
    [
        new(Guid.NewGuid(), "Buy milk", null, false, null, "None", false, "None", new TimeOnly(9, 0))
    ];

    [Fact]
    public async Task Editing_a_shared_list_offline_is_refused_by_the_store_not_only_hidden_on_screen()
    {
        using var store = new LocalStore();
        var repository = new LocalTaskListRepository(store, TimeProvider.System, FixedNetworkStatus.Offline);
        var taskList = await SharedListAsync(store);

        var outcome = await repository.UpdateAsync(taskList.LocalId, new TaskListContent("Edited anyway", SomeItems, IsGroup: false, "Normal"));

        Assert.Equal(LocalWriteOutcome.RefusedWhileOffline, outcome);
    }

    [Fact]
    public async Task A_refused_edit_leaves_nothing_in_the_queue()
    {
        using var store = new LocalStore();
        var repository = new LocalTaskListRepository(store, TimeProvider.System, FixedNetworkStatus.Offline);
        var taskList = await SharedListAsync(store);

        await repository.UpdateAsync(taskList.LocalId, new TaskListContent("Edited anyway", SomeItems, IsGroup: false, "Normal"));

        // Nothing queued means nothing will be replayed over somebody else's work later.
        Assert.Empty(await repository.GetPendingLocalIdsAsync());
    }

    [Fact]
    public async Task Deleting_a_shared_list_offline_is_refused_too()
    {
        using var store = new LocalStore();
        var repository = new LocalTaskListRepository(store, TimeProvider.System, FixedNetworkStatus.Offline);
        var taskList = await SharedListAsync(store);

        Assert.Equal(LocalWriteOutcome.RefusedWhileOffline, await repository.DeleteAsync(taskList.LocalId));
    }

    [Fact]
    public async Task Online_the_same_edit_goes_through_because_the_server_can_hold_a_lock()
    {
        using var store = new LocalStore();
        var repository = new LocalTaskListRepository(store, TimeProvider.System, FixedNetworkStatus.Online);
        var taskList = await SharedListAsync(store);

        Assert.Equal(LocalWriteOutcome.Applied, await repository.UpdateAsync(taskList.LocalId, new TaskListContent("Edited", SomeItems, IsGroup: false, "Normal")));
    }

    [Fact]
    public async Task A_list_nobody_else_can_touch_is_editable_offline()
    {
        using var store = new LocalStore();
        var repository = new LocalTaskListRepository(store, TimeProvider.System, FixedNetworkStatus.Offline);
        var taskList = await repository.CreateAsync("Mine alone", SomeItems);

        Assert.Equal(LocalWriteOutcome.Applied, await repository.UpdateAsync(taskList.LocalId, new TaskListContent("Edited", SomeItems, IsGroup: false, "Normal")));
    }

    [Fact]
    public async Task Asking_whether_a_list_can_be_edited_does_not_edit_it()
    {
        using var store = new LocalStore();
        var repository = new LocalTaskListRepository(store, TimeProvider.System, FixedNetworkStatus.Offline);
        var taskList = await repository.CreateAsync("Mine alone", SomeItems);

        await repository.CanEditAsync(taskList.LocalId);

        // The obvious shortcut - probe by attempting a write - would queue one, which is the opposite of
        // what a read-only check is for. Only the create should be waiting here.
        Assert.Single(await repository.GetPendingLocalIdsAsync());
    }

    [Fact]
    public async Task The_screen_and_the_write_agree_about_a_shared_list()
    {
        using var store = new LocalStore();
        var repository = new LocalTaskListRepository(store, TimeProvider.System, FixedNetworkStatus.Offline);
        var taskList = await SharedListAsync(store);

        Assert.False(await repository.CanEditAsync(taskList.LocalId));
        Assert.Equal(
            LocalWriteOutcome.RefusedWhileOffline,
            await repository.UpdateAsync(taskList.LocalId, new TaskListContent("Edited", SomeItems, IsGroup: false, "Normal")));
    }

    /// <summary>A list the owner shared out - somebody else may be editing it right now.</summary>
    private static async Task<LocalTaskList> SharedListAsync(LocalStore store)
    {
        await using var dbContext = store.CreateDbContext();
        var taskList = new LocalTaskList
        {
            LocalId = Guid.NewGuid(),
            ServerId = Guid.NewGuid(),
            Title = "Shared",
            IsSharedWithOthers = true
        };

        dbContext.TaskLists.Add(taskList);
        await dbContext.SaveChangesAsync();
        return taskList;
    }
}

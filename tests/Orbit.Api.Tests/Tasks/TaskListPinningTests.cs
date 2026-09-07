using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventories;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.SetTaskListPinned;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Covers pinning: whose it is to do, that it is a separate act from editing the list, and that the one
/// list Orbit maintains itself arrives pinned.
/// </summary>
public sealed class TaskListPinningTests
{
    [Fact]
    public async Task An_owner_can_pin_their_own_list()
    {
        var context = new PinningTestContext();
        var taskListId = await context.AddListAsync("Errands");

        Assert.True(await context.SetPinnedAsync(context.OwnerId, taskListId, isPinned: true));
        Assert.True((await context.GetAsync(taskListId))!.IsPinned);
    }

    [Fact]
    public async Task Unpinning_puts_it_back()
    {
        var context = new PinningTestContext();
        var taskListId = await context.AddListAsync("Errands", isPinned: true);

        await context.SetPinnedAsync(context.OwnerId, taskListId, isPinned: false);

        Assert.False((await context.GetAsync(taskListId))!.IsPinned);
    }

    [Fact]
    public async Task Someone_else_cannot_pin_your_list()
    {
        var context = new PinningTestContext();
        var taskListId = await context.AddListAsync("Errands");

        // Pinning moves the card on its owner's own page - a recipient doing it would be rearranging
        // someone else's, which is a different feature and a worse one to arrive at by accident.
        Assert.False(await context.SetPinnedAsync(Guid.NewGuid(), taskListId, isPinned: true));
        Assert.False((await context.GetAsync(taskListId))!.IsPinned);
    }

    [Fact]
    public async Task Pinning_a_list_that_is_gone_is_refused_rather_than_throwing()
    {
        var context = new PinningTestContext();

        Assert.False(await context.SetPinnedAsync(context.OwnerId, Guid.NewGuid(), isPinned: true));
    }

    [Fact]
    public void Pinning_an_already_pinned_list_changes_nothing()
    {
        var taskList = TaskList.Create(Guid.NewGuid(), "Errands", [], isPinned: true);
        var updatedAt = taskList.UpdatedAtUtc;

        taskList.SetPinned(true);

        // Idempotent, so a duplicate click doesn't make the list look freshly touched - which would
        // reorder it under "newest" for no reason.
        Assert.Equal(updatedAt, taskList.UpdatedAtUtc);
    }

    [Fact]
    public void A_new_list_is_not_pinned()
        => Assert.False(TaskList.Create(Guid.NewGuid(), "Errands", []).IsPinned);

    [Fact]
    public void Pinning_is_not_priority()
    {
        // Two different wishes: priority says how much something matters, a pin says keep it where I can
        // see it. A low-priority list can still be the one being worked on today.
        var taskList = TaskList.Create(Guid.NewGuid(), "Errands", [], priority: ItemPriority.Low, isPinned: true);

        Assert.True(taskList.IsPinned);
        Assert.Equal(ItemPriority.Low, taskList.Priority);
    }

    [Fact]
    public async Task The_list_Orbit_maintains_itself_arrives_pinned()
    {
        var context = new InventoryTestContext();
        var ownerId = Guid.NewGuid();
        var inventoryId = context.AddInventory(ownerId, "Pantry");

        await context.TaskListCoordinator.EnsureManagedTaskListAsync(inventoryId, CancellationToken.None);

        var taskList = Assert.Single(await context.TaskRepository.GetAllAsync(ownerId, updatedSinceUtc: null, CancellationToken.None));
        Assert.Equal(RestockTaskNaming.TitleFor("Pantry"), taskList.Title);
        Assert.True(taskList.IsPinned);
    }

    /// <summary>
    /// A recipient's answer goes on their own grant, leaving the owner's list exactly as it was. This
    /// used to be refused outright and the browser kept the answer in localStorage instead, where it did
    /// not follow the reader to a second browser or to their phone.
    /// </summary>
    [Fact]
    public async Task A_recipient_pins_a_shared_list_on_their_own_grant()
    {
        var context = new PinningTestContext();
        var taskListId = await context.AddListAsync("Shopping");
        var recipientId = await context.ShareWithSomebodyAsync(taskListId);

        var pinned = await context.SetPinnedAsync(recipientId, taskListId, isPinned: true);

        Assert.True(pinned);
        Assert.True((await context.GrantFor(taskListId, recipientId))!.IsPinnedByRecipient);
        Assert.False((await context.GetAsync(taskListId))!.IsPinned);
    }

    /// <summary>Somebody with neither the list nor a grant has nothing on their page to arrange.</summary>
    [Fact]
    public async Task A_stranger_still_cannot_pin_a_list_at_all()
    {
        var context = new PinningTestContext();
        var taskListId = await context.AddListAsync("Shopping");

        var pinned = await context.SetPinnedAsync(Guid.NewGuid(), taskListId, isPinned: true);

        Assert.False(pinned);
        Assert.False((await context.GetAsync(taskListId))!.IsPinned);
    }

    private sealed class PinningTestContext
    {
        private readonly InMemoryTaskRepository _taskRepository = new();
        private readonly InMemoryTaskListShareRepository _shareRepository = new();

        public Guid OwnerId { get; } = Guid.NewGuid();

        public async Task<Guid> AddListAsync(string title, bool isPinned = false)
        {
            var taskList = TaskList.Create(OwnerId, title, [], isPinned: isPinned);
            await _taskRepository.AddAsync(taskList, CancellationToken.None);
            return taskList.Id;
        }

        /// <summary>Shares the list and takes the offer up, since only an accepted grant is access.</summary>
        public async Task<Guid> ShareWithSomebodyAsync(Guid taskListId)
        {
            var recipientId = Guid.NewGuid();
            var grant = TaskListShare.Create(taskListId, OwnerId, recipientId);
            grant.MarkAccepted();
            await _shareRepository.AddAsync(grant, CancellationToken.None);
            return recipientId;
        }

        public Task<TaskListShare?> GrantFor(Guid taskListId, Guid recipientId)
            => _shareRepository.FindAcceptedGrantAsync(taskListId, recipientId, CancellationToken.None);

        public Task<bool> SetPinnedAsync(Guid callerId, Guid taskListId, bool isPinned)
            => new SetTaskListPinnedCommandHandler(_taskRepository, _shareRepository)
                .HandleAsync(new SetTaskListPinnedCommand(callerId, taskListId, isPinned), CancellationToken.None);

        public Task<TaskList?> GetAsync(Guid taskListId) => _taskRepository.GetByIdAsync(OwnerId, taskListId, CancellationToken.None);
    }
}

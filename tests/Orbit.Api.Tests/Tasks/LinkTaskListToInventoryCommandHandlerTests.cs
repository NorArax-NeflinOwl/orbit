using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.LinkTaskListToInventory;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Pointing a list at the inventory its work is measured against - the choice behind the stock check's
/// inventory picker, on both clients.
/// </summary>
public sealed class LinkTaskListToInventoryCommandHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    private LinkTaskListToInventoryCommandHandler AHandler()
        => new(_context.TaskRepository, _context.InventoryRepository);

    /// <summary>
    /// Long enough ago that no clock this runs on could read the same value twice - see
    /// InMemoryTaskRepository.PretendItWasLastChanged for why a test needs one at all.
    /// </summary>
    private static DateTimeOffset AMinuteAgo() => DateTimeOffset.UtcNow.AddMinutes(-1);

    private TaskList AList()
    {
        var taskList = TaskList.Create(_userId, "Zakupy", [TaskItem.Create("Flour", null, false)], isGroup: true);
        _context.TaskRepository.AddAsync(taskList, CancellationToken.None).GetAwaiter().GetResult();
        return taskList;
    }

    [Fact]
    public async Task A_list_can_be_pointed_at_an_inventory()
    {
        var taskList = AList();
        var inventoryId = _context.AddInventory(_userId);

        Assert.True(await AHandler().HandleAsync(
            new LinkTaskListToInventoryCommand(_userId, taskList.Id, inventoryId), CancellationToken.None));

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.Equal(inventoryId, stored!.LinkedInventoryId);
    }

    /// <summary>
    /// And says the list changed. It did not, and the choice reached nobody - not another device, and
    /// not even the phone that made it: that screen re-reads what it has stored, and the change feed
    /// gates a list on its own timestamp, so the picker snapped back to "not measured against a
    /// inventory" while the server held the choice all along.
    /// </summary>
    [Fact]
    public async Task Pointing_it_somewhere_says_the_list_changed()
    {
        var taskList = AList();
        var inventoryId = _context.AddInventory(_userId);
        // Aged first - see InMemoryTaskRepository.PretendItWasLastChanged. Comparing against the stamp
        // the list was created with asks whether the clock ticked between two calls made microseconds
        // apart, which is not what this test is about.
        var before = AMinuteAgo();
        _context.TaskRepository.PretendItWasLastChanged(taskList.Id, before);

        await AHandler().HandleAsync(
            new LinkTaskListToInventoryCommand(_userId, taskList.Id, inventoryId), CancellationToken.None);

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.True(stored!.UpdatedAtUtc > before);
    }

    /// <summary>Choosing what was already chosen is not a change, and should not look like one.</summary>
    [Fact]
    public async Task Pointing_it_where_it_already_points_leaves_the_timestamp_alone()
    {
        var taskList = AList();
        var inventoryId = _context.AddInventory(_userId);
        var handler = AHandler();
        await handler.HandleAsync(
            new LinkTaskListToInventoryCommand(_userId, taskList.Id, inventoryId), CancellationToken.None);
        var afterFirst = (await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None))!
            .UpdatedAtUtc;

        await handler.HandleAsync(
            new LinkTaskListToInventoryCommand(_userId, taskList.Id, inventoryId), CancellationToken.None);

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.Equal(afterFirst, stored!.UpdatedAtUtc);
    }

    /// <summary>Measuring against nothing is a choice too, and the same one to carry.</summary>
    [Fact]
    public async Task Pointing_it_at_nothing_says_the_list_changed_as_well()
    {
        var taskList = AList();
        var inventoryId = _context.AddInventory(_userId);
        var handler = AHandler();
        await handler.HandleAsync(
            new LinkTaskListToInventoryCommand(_userId, taskList.Id, inventoryId), CancellationToken.None);
        // Aged for the same reason as the test above: the linking here is setup, and the stamp it leaves
        // is one the unlinking below could tie with.
        var afterLinking = AMinuteAgo();
        _context.TaskRepository.PretendItWasLastChanged(taskList.Id, afterLinking);

        await handler.HandleAsync(
            new LinkTaskListToInventoryCommand(_userId, taskList.Id, null), CancellationToken.None);

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.Null(stored!.LinkedInventoryId);
        Assert.True(stored.UpdatedAtUtc > afterLinking);
    }

    /// <summary>
    /// An inventory the caller cannot read is not one their list may be measured against - the check
    /// would otherwise report on a shelf they have no access to.
    /// </summary>
    [Fact]
    public async Task A_inventory_that_is_not_the_callers_is_refused()
    {
        var taskList = AList();
        var somebodyElses = _context.AddInventory(Guid.NewGuid());

        Assert.False(await AHandler().HandleAsync(
            new LinkTaskListToInventoryCommand(_userId, taskList.Id, somebodyElses), CancellationToken.None));

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.Null(stored!.LinkedInventoryId);
    }

    [Fact]
    public async Task Somebody_elses_list_cannot_be_pointed_anywhere()
    {
        var taskList = AList();
        var inventoryId = _context.AddInventory(_userId);

        Assert.False(await AHandler().HandleAsync(
            new LinkTaskListToInventoryCommand(Guid.NewGuid(), taskList.Id, inventoryId), CancellationToken.None));
    }
}

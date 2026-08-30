using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Tasks;
using Orbit.Core.Tasks.LinkTaskListToWarehouse;
using Xunit;

namespace Orbit.Api.Tests.Tasks;

/// <summary>
/// Pointing a list at the warehouse its work is measured against - the choice behind the stock check's
/// warehouse picker, on both clients.
/// </summary>
public sealed class LinkTaskListToWarehouseCommandHandlerTests
{
    private readonly Guid _userId = Guid.NewGuid();
    private readonly InventoryTestContext _context = new();

    private LinkTaskListToWarehouseCommandHandler AHandler()
        => new(_context.TaskRepository, _context.WarehouseRepository);

    private TaskList AList()
    {
        var taskList = TaskList.Create(_userId, "Zakupy", [TaskItem.Create("Flour", null, false)], isGroup: true);
        _context.TaskRepository.AddAsync(taskList, CancellationToken.None).GetAwaiter().GetResult();
        return taskList;
    }

    [Fact]
    public async Task A_list_can_be_pointed_at_a_warehouse()
    {
        var taskList = AList();
        var warehouseId = _context.AddWarehouse(_userId);

        Assert.True(await AHandler().HandleAsync(
            new LinkTaskListToWarehouseCommand(_userId, taskList.Id, warehouseId), CancellationToken.None));

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.Equal(warehouseId, stored!.LinkedWarehouseId);
    }

    /// <summary>
    /// And says the list changed. It did not, and the choice reached nobody - not another device, and
    /// not even the phone that made it: that screen re-reads what it has stored, and the change feed
    /// gates a list on its own timestamp, so the picker snapped back to "not measured against a
    /// warehouse" while the server held the choice all along.
    /// </summary>
    [Fact]
    public async Task Pointing_it_somewhere_says_the_list_changed()
    {
        var taskList = AList();
        var warehouseId = _context.AddWarehouse(_userId);
        // Copied out rather than read again afterwards: the store hands back the list itself.
        var before = taskList.UpdatedAtUtc;

        await AHandler().HandleAsync(
            new LinkTaskListToWarehouseCommand(_userId, taskList.Id, warehouseId), CancellationToken.None);

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.True(stored!.UpdatedAtUtc > before);
    }

    /// <summary>Choosing what was already chosen is not a change, and should not look like one.</summary>
    [Fact]
    public async Task Pointing_it_where_it_already_points_leaves_the_timestamp_alone()
    {
        var taskList = AList();
        var warehouseId = _context.AddWarehouse(_userId);
        var handler = AHandler();
        await handler.HandleAsync(
            new LinkTaskListToWarehouseCommand(_userId, taskList.Id, warehouseId), CancellationToken.None);
        var afterFirst = (await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None))!
            .UpdatedAtUtc;

        await handler.HandleAsync(
            new LinkTaskListToWarehouseCommand(_userId, taskList.Id, warehouseId), CancellationToken.None);

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.Equal(afterFirst, stored!.UpdatedAtUtc);
    }

    [Fact]
    public async Task A_warehouse_that_is_not_the_callers_is_refused()
    {
        var taskList = AList();
        var somebodyElses = _context.AddWarehouse(Guid.NewGuid());

        Assert.False(await AHandler().HandleAsync(
            new LinkTaskListToWarehouseCommand(_userId, taskList.Id, somebodyElses), CancellationToken.None));

        var stored = await _context.TaskRepository.GetByIdAsync(_userId, taskList.Id, CancellationToken.None);
        Assert.Null(stored!.LinkedWarehouseId);
    }

    [Fact]
    public async Task Somebody_elses_list_cannot_be_pointed_anywhere()
    {
        var taskList = AList();
        var warehouseId = _context.AddWarehouse(_userId);

        Assert.False(await AHandler().HandleAsync(
            new LinkTaskListToWarehouseCommand(Guid.NewGuid(), taskList.Id, warehouseId), CancellationToken.None));
    }
}

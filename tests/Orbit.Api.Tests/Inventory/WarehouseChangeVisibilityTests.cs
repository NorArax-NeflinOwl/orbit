using Microsoft.EntityFrameworkCore;
using Orbit.Api.Tests.TestDoubles;
using Orbit.Core.Inventory;
using Orbit.Core.Notifications;
using Orbit.Data.Repositories;
using Xunit;

namespace Orbit.Api.Tests.Inventory;

/// <summary>
/// Whether a shelf changed on the server ever reaches the devices reading it. Against a real database
/// rather than the in-memory double, because the answer is a column: the change feed hands out
/// warehouses and gates them on the warehouse's own timestamp, while the writes that top a product up
/// or cross an errand off go straight at the item.
///
/// The bug this pins: finishing a restock round filled the shelf and said nothing, so a phone kept
/// showing the old amounts - and its next save wrote them back over the top-up.
/// </summary>
public sealed class WarehouseChangeVisibilityTests : IDisposable
{
    private readonly TemporarySqliteDatabase _database = new();
    private readonly Guid _userId = Guid.NewGuid();

    [Fact]
    public async Task Topping_a_product_up_says_the_warehouse_changed()
    {
        var (warehouses, items, warehouse) = await AShelfAsync();
        var flour = InventoryItem.Create(
            warehouse.Id, "Flour", "Food", "Dry", 0, 5, InventoryUnit.Piece, null, NotificationChannel.None);
        await items.AddAsync(flour, CancellationToken.None);
        var before = await PutTheStampBackAsync(warehouse);

        Assert.True(flour.TopUpToMinimum());
        await items.UpdateAsync(flour, CancellationToken.None);

        Assert.True(await LastChangedAsync(warehouses, warehouse.Id) > before);
    }

    [Fact]
    public async Task Adding_and_removing_a_product_say_so_too()
    {
        var (warehouses, items, warehouse) = await AShelfAsync();
        var beforeAdding = await PutTheStampBackAsync(warehouse);

        var sugar = InventoryItem.Create(
            warehouse.Id, "Sugar", "Food", "Dry", 1, null, InventoryUnit.Piece, null, NotificationChannel.None);
        await items.AddAsync(sugar, CancellationToken.None);

        Assert.True(await LastChangedAsync(warehouses, warehouse.Id) > beforeAdding);

        var beforeRemoving = await PutTheStampBackAsync(warehouse);
        await items.DeleteAsync(warehouse.Id, sugar.Id, CancellationToken.None);

        Assert.True(await LastChangedAsync(warehouses, warehouse.Id) > beforeRemoving);
    }

    // No test here for a shelf nothing happened to staying out of the feed: the delta itself is a
    // DateTimeOffset comparison, which SQLite cannot translate and Postgres can - see
    // WarehouseRepository.GetAllAsync. What these can pin is the timestamp the delta reads.

    private async Task<(WarehouseRepository Warehouses, InventoryRepository Items, Warehouse Warehouse)> AShelfAsync()
    {
        var warehouses = new WarehouseRepository(_database.DbContext);
        var warehouse = Warehouse.Create(_userId, "Pantry");
        await warehouses.AddAsync(warehouse, CancellationToken.None);
        return (warehouses, new InventoryRepository(_database.DbContext), warehouse);
    }

    /// <summary>
    /// Puts the shelf's "last changed" a minute back and hands the value over, so the assertions above
    /// are about the stamp being rewritten rather than about whether the clock ticked between two
    /// writes made microseconds apart - see InMemoryTaskRepository.PretendItWasLastChanged, which says
    /// what was measured.
    /// </summary>
    private async Task<DateTimeOffset> PutTheStampBackAsync(Warehouse warehouse)
    {
        var aMinuteAgo = DateTimeOffset.UtcNow.AddMinutes(-1);
        // Written at the row rather than through the repository: the context is already tracking this
        // warehouse, and handing it a second instance of the same id is an identity conflict rather
        // than an update. These tests are against the real database on purpose, so reaching the column
        // is in keeping.
        var stored = await _database.DbContext.Warehouses.FirstAsync(
            candidate => candidate.Id == warehouse.Id);
        stored.UpdatedAtUtc = aMinuteAgo;
        await _database.DbContext.SaveChangesAsync();

        return aMinuteAgo;
    }

    private async Task<DateTimeOffset> LastChangedAsync(WarehouseRepository warehouses, Guid warehouseId)
        => (await warehouses.GetByIdAsync(_userId, warehouseId, CancellationToken.None))!.UpdatedAtUtc;

    public void Dispose() => _database.Dispose();
}

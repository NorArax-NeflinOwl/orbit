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
        var before = await LastChangedAsync(warehouses, warehouse.Id);

        Assert.True(flour.TopUpToMinimum());
        await items.UpdateAsync(flour, CancellationToken.None);

        Assert.True(await LastChangedAsync(warehouses, warehouse.Id) > before);
    }

    [Fact]
    public async Task Adding_and_removing_a_product_say_so_too()
    {
        var (warehouses, items, warehouse) = await AShelfAsync();
        var createdAt = await LastChangedAsync(warehouses, warehouse.Id);

        var sugar = InventoryItem.Create(
            warehouse.Id, "Sugar", "Food", "Dry", 1, null, InventoryUnit.Piece, null, NotificationChannel.None);
        await items.AddAsync(sugar, CancellationToken.None);
        var afterAdding = await LastChangedAsync(warehouses, warehouse.Id);
        Assert.True(afterAdding > createdAt);

        await items.DeleteAsync(warehouse.Id, sugar.Id, CancellationToken.None);

        Assert.True(await LastChangedAsync(warehouses, warehouse.Id) > afterAdding);
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

    private async Task<DateTimeOffset> LastChangedAsync(WarehouseRepository warehouses, Guid warehouseId)
        => (await warehouses.GetByIdAsync(_userId, warehouseId, CancellationToken.None))!.UpdatedAtUtc;

    public void Dispose() => _database.Dispose();
}

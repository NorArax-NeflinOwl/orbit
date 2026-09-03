using Microsoft.EntityFrameworkCore;
using Orbit.Core.Inventories;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class InventoryManagedTaskListRepository : IInventoryManagedTaskListRepository
{
    private readonly OrbitDbContext _dbContext;

    public InventoryManagedTaskListRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid?> GetTaskListIdAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryManagedTaskLists
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.InventoryId == inventoryId, cancellationToken);

        // A row can exist before any list does: settings are writable for an inventory nothing has gone
        // low in yet (see SetSettingsAsync). An empty id there means "no list", not "the list with the
        // empty id" - which is what every caller here already treats null as.
        return entity is null || entity.TaskListId == Guid.Empty ? null : entity.TaskListId;
    }

    public async Task<Guid?> GetInventoryIdAsync(Guid taskListId, CancellationToken cancellationToken)
    {
        if (taskListId == Guid.Empty)
        {
            // Never a real list, and asking would match every settings-only row - see GetTaskListIdAsync.
            return null;
        }

        var entity = await _dbContext.InventoryManagedTaskLists
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TaskListId == taskListId, cancellationToken);

        return entity?.InventoryId;
    }

    public async Task SetTaskListIdAsync(Guid inventoryId, Guid taskListId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryManagedTaskLists
            .FirstOrDefaultAsync(row => row.InventoryId == inventoryId, cancellationToken);

        if (entity is null)
        {
            _dbContext.InventoryManagedTaskLists.Add(new InventoryManagedTaskListEntity
            {
                Id = Guid.NewGuid(),
                InventoryId = inventoryId,
                TaskListId = taskListId
            });
        }
        else
        {
            entity.TaskListId = taskListId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<RestockListSettings> GetSettingsAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryManagedTaskLists
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.InventoryId == inventoryId, cancellationToken);

        // An inventory with no list yet has the defaults rather than nothing, so nobody has to tell "not
        // set" from "set to what everybody starts with".
        return entity is null
            ? RestockListSettings.Default
            : new RestockListSettings(
                entity.OnlyLinkedWithDueDate,
                TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(entity.RefreshTimeOfDayMinutes)));
    }

    /// <summary>
    /// Writes the settings even for an inventory whose list has not been created yet: somebody can decide
    /// how the list should behave before anything has gone low enough to make one.
    /// </summary>
    public async Task SetSettingsAsync(Guid inventoryId, RestockListSettings settings, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryManagedTaskLists
            .FirstOrDefaultAsync(row => row.InventoryId == inventoryId, cancellationToken);

        if (entity is null)
        {
            entity = new InventoryManagedTaskListEntity { Id = Guid.NewGuid(), InventoryId = inventoryId };
            _dbContext.InventoryManagedTaskLists.Add(entity);
        }

        entity.OnlyLinkedWithDueDate = settings.OnlyLinkedWithDueDate;
        entity.RefreshTimeOfDayMinutes = settings.RefreshTimeOfDay.Hour * 60 + settings.RefreshTimeOfDay.Minute;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

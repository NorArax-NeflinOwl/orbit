using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Inventories;
using Orbit.Core.Notifications;
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

    /// <summary>
    /// Empties the id rather than deleting the row: the settings beside it are the reader's and outlive
    /// the list - see GetTaskListIdAsync, which already reads an empty id as "no list".
    /// </summary>
    public async Task ClearTaskListIdAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryManagedTaskLists
            .FirstOrDefaultAsync(row => row.InventoryId == inventoryId, cancellationToken);
        if (entity is null)
        {
            return;
        }

        entity.TaskListId = Guid.Empty;
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
                TimeOnly.FromTimeSpan(TimeSpan.FromMinutes(entity.RefreshTimeOfDayMinutes)),
                entity.IsEnabled,
                entity.RemindDaily,
                // A priority nobody recognises reads as Normal rather than failing the whole settings
                // read - the same rule every other stored-by-name enum here follows.
                Enum.TryParse<ItemPriority>(entity.ListPriority, out var priority) ? priority : ItemPriority.Normal,
                entity.OnlyCheckedRegularly,
                Enum.TryParse<NotificationChannel>(entity.ReminderNotificationChannel, out var channel)
                    ? channel
                    : NotificationChannel.Push);
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
        entity.IsEnabled = settings.IsEnabled;
        entity.RemindDaily = settings.RemindDaily;
        entity.ListPriority = settings.ListPriority.ToString();
        entity.OnlyCheckedRegularly = settings.OnlyCheckedRegularly;
        entity.ReminderNotificationChannel = settings.ReminderChannel.ToString();
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

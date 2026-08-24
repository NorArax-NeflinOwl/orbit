using Microsoft.EntityFrameworkCore;
using Orbit.Core.Inventory;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class InventoryManagedTaskListRepository : IInventoryManagedTaskListRepository
{
    private readonly OrbitDbContext _dbContext;

    public InventoryManagedTaskListRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid?> GetTaskListIdAsync(Guid userId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryManagedTaskLists
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);

        return entity?.TaskListId;
    }

    public async Task SetTaskListIdAsync(Guid userId, Guid taskListId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.InventoryManagedTaskLists
            .FirstOrDefaultAsync(row => row.UserId == userId, cancellationToken);

        if (entity is null)
        {
            _dbContext.InventoryManagedTaskLists.Add(new InventoryManagedTaskListEntity
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                TaskListId = taskListId
            });
        }
        else
        {
            entity.TaskListId = taskListId;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}

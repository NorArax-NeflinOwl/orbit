using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Tasks;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class TaskListShareRepository : ITaskListShareRepository
{
    private readonly OrbitDbContext _dbContext;

    public TaskListShareRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(TaskListShare share, CancellationToken cancellationToken)
    {
        _dbContext.TaskShares.Add(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TaskListShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.TaskShares
            .AsNoTracking()
            .FirstOrDefaultAsync(share => share.Id == id && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task UpdateAsync(TaskListShare share, CancellationToken cancellationToken)
    {
        _dbContext.TaskShares.Update(ToEntity(share));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<TaskListShare?> FindExistingAsync(Guid sourceTaskListId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.TaskShares
            .AsNoTracking()
            .FirstOrDefaultAsync(share => share.SourceTaskListId == sourceTaskListId && share.RecipientUserId == recipientUserId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<TaskListShare?> FindAcceptedGrantAsync(Guid sourceTaskListId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.TaskShares
            .AsNoTracking()
            .FirstOrDefaultAsync(
                share => share.SourceTaskListId == sourceTaskListId && share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null,
                cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task<IReadOnlyList<TaskListShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.TaskShares
            .AsNoTracking()
            .Where(share => share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    private static TaskListShare ToDomain(TaskShareEntity entity)
        => TaskListShare.FromPersistence(
            entity.Id, entity.SourceTaskListId, entity.OwnerUserId, entity.RecipientUserId,
            Enum.Parse<ShareAccessLevel>(entity.AccessLevel), entity.CreatedAtUtc, entity.AcceptedAtUtc);

    private static TaskShareEntity ToEntity(TaskListShare share)
        => new()
        {
            Id = share.Id,
            SourceTaskListId = share.SourceTaskListId,
            OwnerUserId = share.OwnerUserId,
            RecipientUserId = share.RecipientUserId,
            AccessLevel = share.AccessLevel.ToString(),
            CreatedAtUtc = share.CreatedAtUtc,
            AcceptedAtUtc = share.AcceptedAtUtc
        };
    public async Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken)
    {
        await _dbContext.TaskShares
            .Where(share => share.SourceTaskListId == sourceId && share.RecipientUserId == recipientUserId && share.AcceptedAtUtc != null)
            .ExecuteDeleteAsync(cancellationToken);
    }
}
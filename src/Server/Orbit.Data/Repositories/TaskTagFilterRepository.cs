using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Orbit.Core.Tasks.TagFilters;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class TaskTagFilterRepository : ITaskTagFilterRepository
{
    private readonly OrbitDbContext _dbContext;

    public TaskTagFilterRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TaskTagFilter>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        var rows = await _dbContext.TaskTagFilters
            .AsNoTracking()
            .Where(row => row.UserId == userId)
            .OrderBy(row => row.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return [.. rows.Select(row => new TaskTagFilter(
            row.Id, JsonSerializer.Deserialize<List<string>>(row.TagsJson) ?? [], row.MatchesAll, row.CreatedAtUtc))];
    }

    public async Task AddAsync(Guid userId, TaskTagFilter filter, CancellationToken cancellationToken)
    {
        _dbContext.TaskTagFilters.Add(new TaskTagFilterEntity
        {
            Id = filter.Id,
            UserId = userId,
            TagsJson = JsonSerializer.Serialize(filter.Tags),
            MatchesAll = filter.MatchesAll,
            CreatedAtUtc = filter.CreatedAtUtc
        });
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid filterId, CancellationToken cancellationToken)
        => await _dbContext.TaskTagFilters
            .Where(row => row.UserId == userId && row.Id == filterId)
            .ExecuteDeleteAsync(cancellationToken) > 0;
}

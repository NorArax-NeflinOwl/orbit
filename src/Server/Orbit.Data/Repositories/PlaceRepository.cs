using Microsoft.EntityFrameworkCore;
using Orbit.Core.Abstractions;
using Orbit.Core.Calendar;
using Orbit.Core.Places;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

/// <summary>Mirrors NoteRepository - see it for why the delta cursor is applied in the database.</summary>
public sealed class PlaceRepository : IPlaceRepository
{
    private readonly OrbitDbContext _dbContext;

    public PlaceRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<Place>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken)
    {
        var query = _dbContext.Places
            .AsNoTracking()
            .Include(place => place.TaskLists)
            .Where(entity => entity.UserId == userId);

        if (updatedSinceUtc is not null)
        {
            query = query.Where(entity => entity.UpdatedAtUtc >= updatedSinceUtc.Value);
        }

        var entities = await query.ToListAsync(cancellationToken);
        return entities
            .OrderByDescending(entity => entity.UpdatedAtUtc)
            .Select(ToDomain)
            .ToList();
    }

    public async Task<Place?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Places
            .AsNoTracking()
            .Include(place => place.TaskLists)
            .FirstOrDefaultAsync(place => place.Id == id && place.UserId == userId, cancellationToken);

        return entity is null ? null : ToDomain(entity);
    }

    public async Task AddAsync(Place place, CancellationToken cancellationToken)
    {
        _dbContext.Places.Add(ToEntity(place));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// The links are replaced rather than diffed, the same way a task list's entries are: the domain
    /// hands back the whole set, and clearing a tracked navigation instead of removing and adding is what
    /// made EF treat freshly-built rows as updates to rows that do not exist yet.
    /// </summary>
    public async Task UpdateAsync(Place place, CancellationToken cancellationToken)
    {
        var entity = await _dbContext.Places
            .Include(stored => stored.TaskLists)
            .FirstAsync(stored => stored.Id == place.Id, cancellationToken);

        entity.Name = place.Name;
        entity.Description = place.Description;
        entity.Address = place.Where.Address ?? string.Empty;
        entity.Latitude = place.Where.Latitude;
        entity.Longitude = place.Where.Longitude;
        entity.Colour = place.Colour;
        entity.Priority = place.Priority.ToString();
        entity.UpdatedAtUtc = place.UpdatedAtUtc;

        _dbContext.RemoveRange(entity.TaskLists);
        _dbContext.AddRange(LinksOf(place));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken)
    {
        var removed = await _dbContext.Places
            .Where(place => place.Id == id && place.UserId == userId)
            .ExecuteDeleteAsync(cancellationToken);
        return removed > 0;
    }

    private static Place ToDomain(PlaceEntity entity)
        => Place.FromPersistence(
            entity.Id,
            entity.UserId,
            entity.Name,
            entity.Description,
            new EventLocation(
                entity.Address.Length == 0 ? null : entity.Address, entity.Latitude, entity.Longitude),
            entity.Colour,
            // Anything unreadable falls back to Normal, the way every other stored-by-name enum here
            // does: a row must not throw while being read.
            Enum.TryParse<ItemPriority>(entity.Priority, out var priority) ? priority : ItemPriority.Normal,
            [.. entity.TaskLists.OrderBy(link => link.Position).Select(link => link.TaskListId)],
            entity.CreatedAtUtc,
            entity.UpdatedAtUtc);

    private static PlaceEntity ToEntity(Place place)
        => new()
        {
            Id = place.Id,
            UserId = place.UserId,
            Name = place.Name,
            Description = place.Description,
            Address = place.Where.Address ?? string.Empty,
            Latitude = place.Where.Latitude,
            Longitude = place.Where.Longitude,
            Colour = place.Colour,
            Priority = place.Priority.ToString(),
            TaskLists = [.. LinksOf(place)],
            CreatedAtUtc = place.CreatedAtUtc,
            UpdatedAtUtc = place.UpdatedAtUtc
        };

    private static IEnumerable<PlaceTaskListLinkEntity> LinksOf(Place place)
        => place.TaskListIds.Select((taskListId, position) => new PlaceTaskListLinkEntity
        {
            PlaceId = place.Id,
            TaskListId = taskListId,
            Position = position
        });
}

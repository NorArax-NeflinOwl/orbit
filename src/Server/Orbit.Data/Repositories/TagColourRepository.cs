using Microsoft.EntityFrameworkCore;
using Orbit.Core.Tags;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class TagColourRepository : ITagColourRepository
{
    private readonly OrbitDbContext _dbContext;

    public TagColourRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<TagColour>> GetAllAsync(Guid userId, CancellationToken cancellationToken)
        => await _dbContext.TagColours
            .AsNoTracking()
            .Where(row => row.UserId == userId)
            .OrderBy(row => row.NormalizedTag)
            .Select(row => new TagColour(row.Tag, row.Colour))
            .ToListAsync(cancellationToken);

    public async Task SetAsync(Guid userId, string tag, string colour, CancellationToken cancellationToken)
    {
        var key = TagNames.KeyOf(tag);
        var row = await _dbContext.TagColours
            .FirstOrDefaultAsync(stored => stored.UserId == userId && stored.NormalizedTag == key, cancellationToken);
        if (row is null)
        {
            _dbContext.TagColours.Add(new TagColourEntity
            {
                UserId = userId,
                NormalizedTag = key,
                Tag = tag.Trim(),
                Colour = colour,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            });
        }
        else
        {
            row.Tag = tag.Trim();
            row.Colour = colour;
            row.UpdatedAtUtc = DateTimeOffset.UtcNow;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAsync(Guid userId, string tag, CancellationToken cancellationToken)
    {
        var key = TagNames.KeyOf(tag);
        await _dbContext.TagColours
            .Where(stored => stored.UserId == userId && stored.NormalizedTag == key)
            .ExecuteDeleteAsync(cancellationToken);
    }
}

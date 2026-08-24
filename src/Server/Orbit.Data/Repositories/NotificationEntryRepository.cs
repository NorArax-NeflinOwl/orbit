using Microsoft.EntityFrameworkCore;
using Orbit.Core.Notifications;
using Orbit.Data.Entities;

namespace Orbit.Data.Repositories;

public sealed class NotificationEntryRepository : INotificationEntryRepository
{
    private readonly OrbitDbContext _dbContext;

    public NotificationEntryRepository(OrbitDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(NotificationEntry entry, CancellationToken cancellationToken)
    {
        _dbContext.NotificationEntries.Add(ToEntity(entry));
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<NotificationEntry>> GetRecentAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.NotificationEntries
            .AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .OrderByDescending(entity => entity.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
        => _dbContext.NotificationEntries
            .AsNoTracking()
            .CountAsync(entity => entity.UserId == userId && entity.ReadAtUtc == null, cancellationToken);

    public async Task MarkAllReadAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        var unreadEntities = await _dbContext.NotificationEntries
            .Where(entity => entity.UserId == userId && entity.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

        foreach (var entity in unreadEntities)
        {
            entity.ReadAtUtc = nowUtc;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private static NotificationEntry ToDomain(NotificationEntryEntity entity)
        => NotificationEntry.FromPersistence(
            entity.Id, entity.UserId, Enum.Parse<NotificationEntryKind>(entity.Kind, ignoreCase: true),
            entity.Title, entity.Body, entity.Url, entity.CreatedAtUtc, entity.ReadAtUtc);

    private static NotificationEntryEntity ToEntity(NotificationEntry entry)
        => new()
        {
            Id = entry.Id,
            UserId = entry.UserId,
            Kind = entry.Kind.ToString(),
            Title = entry.Title,
            Body = entry.Body,
            Url = entry.Url,
            CreatedAtUtc = entry.CreatedAtUtc,
            ReadAtUtc = entry.ReadAtUtc
        };
}

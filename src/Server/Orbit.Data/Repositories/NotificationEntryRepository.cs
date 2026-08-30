using System.Text.Json;
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
            .Where(entity => entity.UserId == userId && entity.DismissedAtUtc == null)
            .OrderByDescending(entity => entity.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<NotificationEntry>> GetHistoryAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.NotificationEntries
            .AsNoTracking()
            .Where(entity => entity.UserId == userId)
            .OrderByDescending(entity => entity.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

    public async Task<IReadOnlyList<NotificationEntry>> GetUnreadAsync(Guid userId, int take, CancellationToken cancellationToken)
    {
        var entities = await _dbContext.NotificationEntries
            .AsNoTracking()
            .Where(entity => entity.UserId == userId && entity.ReadAtUtc == null)
            .OrderByDescending(entity => entity.CreatedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken);

        return entities.Select(ToDomain).ToList();
    }

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

    public async Task MarkReadByUrlAsync(Guid userId, string url, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        => await _dbContext.NotificationEntries
            .Where(entity => entity.UserId == userId && entity.ReadAtUtc == null && entity.Url == url)
            .ExecuteUpdateAsync(entity => entity.SetProperty(row => row.ReadAtUtc, nowUtc), cancellationToken);

    public async Task DismissAllAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
        => await _dbContext.NotificationEntries
            .Where(entity => entity.UserId == userId && entity.DismissedAtUtc == null)
            .ExecuteUpdateAsync(
                entity => entity
                    .SetProperty(row => row.DismissedAtUtc, nowUtc)
                    // Dismissed implies read - see NotificationEntry.Dismiss for why the badge must not
                    // outlive the panel it points at.
                    .SetProperty(row => row.ReadAtUtc, row => row.ReadAtUtc ?? nowUtc),
                cancellationToken);

    /// <summary>
    /// Grouped by retention window rather than run per user: readers who chose the same number of days
    /// share one cutoff, so this stays a handful of deletes however many accounts exist.
    /// </summary>
    public async Task<int> DeleteExpiredAsync(DateTimeOffset nowUtc, TimeSpan defaultRetention, CancellationToken cancellationToken)
    {
        var configuredRetentions = await _dbContext.NotificationSettings
            .AsNoTracking()
            .Select(settings => new { settings.UserId, settings.RetentionDays })
            .ToListAsync(cancellationToken);

        var deletedCount = 0;
        foreach (var retentionGroup in configuredRetentions.GroupBy(settings => settings.RetentionDays))
        {
            var userIds = retentionGroup.Select(settings => settings.UserId).ToList();
            var cutoffUtc = nowUtc.AddDays(-retentionGroup.Key);
            deletedCount += await _dbContext.NotificationEntries
                .Where(entry => userIds.Contains(entry.UserId) && entry.CreatedAtUtc < cutoffUtc)
                .ExecuteDeleteAsync(cancellationToken);
        }

        // Everyone else has never saved notification settings, so the default window applies to them.
        var configuredUserIds = configuredRetentions.Select(settings => settings.UserId).ToList();
        var defaultCutoffUtc = nowUtc - defaultRetention;
        deletedCount += await _dbContext.NotificationEntries
            .Where(entry => !configuredUserIds.Contains(entry.UserId) && entry.CreatedAtUtc < defaultCutoffUtc)
            .ExecuteDeleteAsync(cancellationToken);

        return deletedCount;
    }

    private static NotificationEntry ToDomain(NotificationEntryEntity entity)
        => NotificationEntry.FromPersistence(
            entity.Id, entity.UserId, Enum.Parse<NotificationEntryKind>(entity.Kind, ignoreCase: true),
            entity.Title, Read(entity.TitleArguments), entity.Body, Read(entity.BodyArguments), entity.Url,
            entity.CreatedAtUtc, entity.ReadAtUtc, entity.DismissedAtUtc);

    /// <summary>Nothing stored means nothing to fill in - the format is already the sentence.</summary>
    private static IReadOnlyList<string> Read(string? arguments)
        => arguments is null ? [] : JsonSerializer.Deserialize<List<string>>(arguments) ?? [];

    /// <summary>And nothing to fill in is stored as nothing, not as an empty array.</summary>
    private static string? Write(IReadOnlyList<string> arguments)
        => arguments.Count == 0 ? null : JsonSerializer.Serialize(arguments);

    private static NotificationEntryEntity ToEntity(NotificationEntry entry)
        => new()
        {
            Id = entry.Id,
            UserId = entry.UserId,
            Kind = entry.Kind.ToString(),
            Title = entry.Title,
            TitleArguments = Write(entry.TitleArguments),
            Body = entry.Body,
            BodyArguments = Write(entry.BodyArguments),
            Url = entry.Url,
            CreatedAtUtc = entry.CreatedAtUtc,
            ReadAtUtc = entry.ReadAtUtc,
            DismissedAtUtc = entry.DismissedAtUtc
        };
}

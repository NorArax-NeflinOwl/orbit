using Orbit.Core.Notifications;

namespace Orbit.Api.Tests.TestDoubles;

/// <summary>In-memory <see cref="INotificationEntryRepository"/> stub for unit tests.</summary>
internal sealed class InMemoryNotificationEntryRepository : INotificationEntryRepository
{
    private readonly List<NotificationEntry> _entries = [];

    public Task AddAsync(NotificationEntry entry, CancellationToken cancellationToken)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<NotificationEntry>> GetRecentAsync(Guid userId, int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<NotificationEntry>>(
            _entries.Where(entry => entry.UserId == userId && !entry.IsDismissed)
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Take(take)
                .ToList());

    public Task<IReadOnlyList<NotificationEntry>> GetHistoryAsync(Guid userId, int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<NotificationEntry>>(
            _entries.Where(entry => entry.UserId == userId)
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Take(take)
                .ToList());

    public Task<IReadOnlyList<NotificationEntry>> GetUnreadAsync(Guid userId, int take, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<NotificationEntry>>(
            _entries.Where(entry => entry.UserId == userId && !entry.IsRead)
                .OrderByDescending(entry => entry.CreatedAtUtc)
                .Take(take)
                .ToList());

    public Task DismissAllAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        foreach (var entry in _entries.Where(entry => entry.UserId == userId))
        {
            entry.Dismiss(nowUtc);
        }

        return Task.CompletedTask;
    }

    public Task MarkReadByUrlAsync(Guid userId, string url, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        foreach (var entry in _entries.Where(entry =>
            entry.UserId == userId && !entry.IsRead && string.Equals(entry.Url, url, StringComparison.Ordinal)))
        {
            entry.MarkRead(nowUtc);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Takes the retention window as a parameter rather than reading a settings repository, unlike the
    /// real one: a test that wants a per-user window sets RetentionDaysByUser below.
    /// </summary>
    public Task<int> DeleteExpiredAsync(DateTimeOffset nowUtc, TimeSpan defaultRetention, CancellationToken cancellationToken)
    {
        var deletedCount = _entries.RemoveAll(entry =>
        {
            var retention = RetentionDaysByUser.TryGetValue(entry.UserId, out var days)
                ? TimeSpan.FromDays(days)
                : defaultRetention;
            return entry.CreatedAtUtc < nowUtc - retention;
        });

        return Task.FromResult(deletedCount);
    }

    /// <summary>Per-user retention windows, standing in for the settings rows the real repository joins against.</summary>
    public Dictionary<Guid, int> RetentionDaysByUser { get; } = [];

    public Task MarkAllReadAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        foreach (var entry in _entries.Where(entry => entry.UserId == userId && !entry.IsRead))
        {
            entry.MarkRead(nowUtc);
        }

        return Task.CompletedTask;
    }
}

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

    public Task DeleteAllAsync(Guid userId, CancellationToken cancellationToken)
    {
        _entries.RemoveAll(entry => entry.UserId == userId);
        return Task.CompletedTask;
    }

    public Task MarkAllReadAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        foreach (var entry in _entries.Where(entry => entry.UserId == userId && !entry.IsRead))
        {
            entry.MarkRead(nowUtc);
        }

        return Task.CompletedTask;
    }
}

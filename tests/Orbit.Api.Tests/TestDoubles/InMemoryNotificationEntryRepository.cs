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

    public Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken)
        => Task.FromResult(_entries.Count(entry => entry.UserId == userId && !entry.IsRead));

    public Task MarkAllReadAsync(Guid userId, DateTimeOffset nowUtc, CancellationToken cancellationToken)
    {
        foreach (var entry in _entries.Where(entry => entry.UserId == userId && !entry.IsRead))
        {
            entry.MarkRead(nowUtc);
        }

        return Task.CompletedTask;
    }
}

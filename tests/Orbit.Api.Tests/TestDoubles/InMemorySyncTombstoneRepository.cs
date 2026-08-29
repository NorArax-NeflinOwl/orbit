using Orbit.Core.Sync;

namespace Orbit.Api.Tests.TestDoubles;

internal sealed class InMemorySyncTombstoneRepository : ISyncTombstoneRepository
{
    private readonly List<SyncTombstone> _tombstones = [];

    public IReadOnlyList<SyncTombstone> Tombstones => _tombstones;

    public Task RecordAsync(SyncTombstone tombstone, CancellationToken cancellationToken)
    {
        _tombstones.Add(tombstone);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Guid>> GetDeletedIdsSinceAsync(
        Guid userId, string entityType, DateTimeOffset sinceUtc, CancellationToken cancellationToken)
        => Task.FromResult<IReadOnlyList<Guid>>(_tombstones
            .Where(tombstone => tombstone.UserId == userId
                && tombstone.EntityType == entityType
                && tombstone.DeletedAtUtc >= sinceUtc)
            .Select(tombstone => tombstone.EntityId)
            .ToList());
}

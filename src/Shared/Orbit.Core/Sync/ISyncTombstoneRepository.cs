namespace Orbit.Core.Sync;

public interface ISyncTombstoneRepository
{
    Task RecordAsync(SyncTombstone tombstone, CancellationToken cancellationToken);

    /// <summary>
    /// Ids of <paramref name="entityType"/> things this user deleted at or after <paramref name="sinceUtc"/>.
    /// Inclusive on purpose - see SyncChanges' note on why the cursor overlaps rather than risking a gap.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDeletedIdsSinceAsync(
        Guid userId, string entityType, DateTimeOffset sinceUtc, CancellationToken cancellationToken);
}

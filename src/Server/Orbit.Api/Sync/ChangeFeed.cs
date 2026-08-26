using Orbit.Contracts.Sync;
using Orbit.Core.Sync;

namespace Orbit.Api.Sync;

/// <summary>
/// Assembles the "what changed since" answer every collection's /changes endpoint returns, so the four
/// of them agree on the cursor format and on how deletions are looked up rather than each re-deriving it.
/// </summary>
internal static class ChangeFeed
{
    /// <summary>
    /// Call this before querying, not after: anything written while the query runs then falls on or
    /// after the cursor the client comes back with, rather than into the gap between them.
    /// </summary>
    public static DateTimeOffset StartCursor() => DateTimeOffset.UtcNow;

    public static async Task<ChangeFeedDto<TItem>> BuildAsync<TItem>(
        IReadOnlyList<TItem> changed, DateTimeOffset cursor, Guid userId, string entityType, DateTimeOffset since,
        ISyncTombstoneRepository tombstones, CancellationToken cancellationToken)
    {
        var deletedIds = await tombstones.GetDeletedIdsSinceAsync(userId, entityType, since, cancellationToken);
        // "O" on a UTC DateTime ends in "Z"; the same format on a DateTimeOffset would end in "+00:00",
        // which a client pasting it into the next URL would turn into a space. See ChangeFeedDto.
        return new ChangeFeedDto<TItem>(changed, deletedIds, cursor.UtcDateTime.ToString("O"));
    }
}

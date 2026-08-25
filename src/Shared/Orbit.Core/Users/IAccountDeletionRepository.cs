namespace Orbit.Core.Users;

/// <summary>
/// Wipes every row a deleted account owns across the whole schema, in one transaction - a cross-cutting
/// concern that doesn't belong on any single aggregate's own repository (notes, tasks, calendar events, ...),
/// unlike deleting a single one of those. See DeleteAccountCommandHandler, its only caller.
///
/// Dangling references left in *other* users' data (an accepted share, a chat message, a contact entry)
/// are left as-is rather than cleaned up - the same trade-off DeleteWarehouseCommandHandler already
/// makes for a single deleted warehouse, since the resolvers reading those references already treat a
/// stale/missing owner as "not found".
/// </summary>
public interface IAccountDeletionRepository
{
    Task DeleteAllDataForUserAsync(Guid userId, CancellationToken cancellationToken);
}

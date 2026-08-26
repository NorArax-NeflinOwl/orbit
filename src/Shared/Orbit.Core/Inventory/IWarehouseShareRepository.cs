namespace Orbit.Core.Inventory;

/// <summary>Direct mirror of INoteShareRepository - see its member comments for the reasoning behind each lookup.</summary>
public interface IWarehouseShareRepository
{
    Task AddAsync(WarehouseShare share, CancellationToken cancellationToken);

    /// <summary>Scoped to recipientUserId: returns null both when the share doesn't exist and when it was offered to someone else.</summary>
    Task<WarehouseShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(WarehouseShare share, CancellationToken cancellationToken);

    /// <summary>The share already offered for sourceWarehouseId to recipientUserId - accepted or still pending - so a re-share becomes a reminder instead of a duplicate row.</summary>
    Task<WarehouseShare?> FindExistingAsync(Guid sourceWarehouseId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>The *accepted* grant, which is what WarehouseAccessResolver treats as current access.</summary>
    Task<WarehouseShare?> FindAcceptedGrantAsync(Guid sourceWarehouseId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Every warehouse recipientUserId has accepted access to, regardless of owner - see WarehouseAccessResolver.ResolveAllAsync.</summary>
    Task<IReadOnlyList<WarehouseShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of ownerUserId's own warehouses somebody else currently holds accepted access to - the owner's
    /// side of the relationship, which nothing else exposes. Mirrors INoteShareRepository's method of the
    /// same shape, and exists for the same reason: a mobile client cannot hold an edit lock, so anything
    /// another person can change is read-only while offline (info/orbit-maui-plan.md §5.4).
    ///
    /// A whole set in one query, because the caller asks it of every warehouse in a list.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetSharedOutWarehouseIdsAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the accepted grant that puts this warehouse on recipientUserId's list, taking it off their
    /// list without touching the owner's. Scoped to the recipient, so it can only ever remove their own
    /// access. A no-op when there is no such grant.
    /// </summary>
    Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken);
}

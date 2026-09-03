namespace Orbit.Core.Inventories;

/// <summary>Direct mirror of INoteShareRepository - see its member comments for the reasoning behind each lookup.</summary>
public interface IInventoryShareRepository
{
    Task AddAsync(InventoryShare share, CancellationToken cancellationToken);

    /// <summary>Scoped to recipientUserId: returns null both when the share doesn't exist and when it was offered to someone else.</summary>
    Task<InventoryShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(InventoryShare share, CancellationToken cancellationToken);

    /// <summary>The share already offered for sourceInventoryId to recipientUserId - accepted or still pending - so a re-share becomes a reminder instead of a duplicate row.</summary>
    Task<InventoryShare?> FindExistingAsync(Guid sourceInventoryId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>The *accepted* grant, which is what InventoryAccessResolver treats as current access.</summary>
    Task<InventoryShare?> FindAcceptedGrantAsync(Guid sourceInventoryId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Every inventory recipientUserId has accepted access to, regardless of owner - see InventoryAccessResolver.ResolveAllAsync.</summary>
    Task<IReadOnlyList<InventoryShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of ownerUserId's own inventories somebody else currently holds accepted access to - the owner's
    /// side of the relationship, which nothing else exposes. Mirrors INoteShareRepository's method of the
    /// same shape, and exists for the same reason: a mobile client cannot hold an edit lock, so anything
    /// another person can change is read-only while offline (info/orbit-maui-plan.md §5.4).
    ///
    /// A whole set in one query, because the caller asks it of every inventory in a list.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetSharedOutInventoryIdsAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the accepted grant that puts this inventory on recipientUserId's list, taking it off their
    /// list without touching the owner's. Scoped to the recipient, so it can only ever remove their own
    /// access. A no-op when there is no such grant.
    /// </summary>
    Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken);
}

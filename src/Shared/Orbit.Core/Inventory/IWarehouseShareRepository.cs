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
}

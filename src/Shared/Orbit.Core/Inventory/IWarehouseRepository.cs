namespace Orbit.Core.Inventory;

public interface IWarehouseRepository
{
    /// <summary>Every warehouse userId owns. Warehouses shared *with* them come from IWarehouseShareRepository - see WarehouseAccessResolver.</summary>
    /// <summary>
    /// Everything userId owns, or - when updatedSinceUtc is given - only what changed at or after it.
    /// The cursor is applied in the database: a client catching up asks for a delta, and answering it by
    /// fetching everything and discarding most of it saved the wire and nothing else.
    /// </summary>
    Task<IReadOnlyList<Warehouse>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken);

    /// <summary>Scoped to userId as owner - returns null both when the warehouse doesn't exist and when someone else owns it.</summary>
    Task<Warehouse?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(Warehouse warehouse, CancellationToken cancellationToken);

    Task UpdateAsync(Warehouse warehouse, CancellationToken cancellationToken);
    /// <summary>
    /// Writes only who holds the edit lock and until when - see Orbit.Core.Notes.INoteRepository.UpdateLockAsync
    /// for why. Here it saves rewriting every shelf row as well as the warehouse's own.
    /// </summary>
    Task UpdateLockAsync(Warehouse warehouse, CancellationToken cancellationToken);

    /// <summary>Deletes the warehouse along with every item in it - see DeleteWarehouseCommandHandler for why the items go too.</summary>
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    /// <summary>The owner of warehouseId regardless of who is asking, for the paths that need to act as the owner (restock task lists, expiry reminders).</summary>
    Task<Guid?> GetOwnerUserIdAsync(Guid warehouseId, CancellationToken cancellationToken);
}

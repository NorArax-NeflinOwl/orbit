namespace Orbit.Core.Inventories;

public interface IInventoryRepository
{
    /// <summary>Every inventory userId owns. Inventories shared *with* them come from IInventoryShareRepository - see InventoryAccessResolver.</summary>
    /// <summary>
    /// Everything userId owns, or - when updatedSinceUtc is given - only what changed at or after it.
    /// The cursor is applied in the database: a client catching up asks for a delta, and answering it by
    /// fetching everything and discarding most of it saved the wire and nothing else.
    /// </summary>
    Task<IReadOnlyList<Inventory>> GetAllAsync(
        Guid userId, DateTimeOffset? updatedSinceUtc, CancellationToken cancellationToken);

    /// <summary>Scoped to userId as owner - returns null both when the inventory doesn't exist and when someone else owns it.</summary>
    Task<Inventory?> GetByIdAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task AddAsync(Inventory inventory, CancellationToken cancellationToken);

    Task UpdateAsync(Inventory inventory, CancellationToken cancellationToken);
    /// <summary>
    /// Writes only who holds the edit lock and until when - see Orbit.Core.Notes.INoteRepository.UpdateLockAsync
    /// for why. Here it saves rewriting every shelf row as well as the inventory's own.
    /// </summary>
    Task UpdateLockAsync(Inventory inventory, CancellationToken cancellationToken);

    /// <summary>Deletes the inventory along with every item in it - see DeleteInventoryCommandHandler for why the items go too.</summary>
    Task DeleteAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    /// <summary>The owner of inventoryId regardless of who is asking, for the paths that need to act as the owner (restock task lists, expiry reminders).</summary>
    Task<Guid?> GetOwnerUserIdAsync(Guid inventoryId, CancellationToken cancellationToken);
}

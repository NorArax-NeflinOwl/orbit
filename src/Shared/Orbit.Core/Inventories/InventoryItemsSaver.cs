namespace Orbit.Core.Inventories;

/// <summary>
/// Writing an inventory's whole item list, for the two commands that do it - creating one that already
/// has rows on it, and saving one that already exists.
///
/// Its own class rather than a method on the update handler, because the interesting part is not the
/// writing: it is that an inventory item carries state the editor never sees - its open restock task -
/// so the rows are reconciled rather than replaced. A second copy of that rule in the create handler
/// would be a second chance to get it wrong, and the way the two paths came apart in the first place
/// was that creating an inventory did not write items at all and refused a caller that sent any.
/// </summary>
public sealed class InventoryItemsSaver(
    IInventoryItemRepository inventoryItemRepository, InventoryTaskListCoordinator taskListCoordinator)
{
    /// <summary>
    /// Makes the shelf hold exactly <paramref name="items"/>: rows missing from it are deleted, rows
    /// carrying an id are updated in place, and the rest are added. The order they arrive in is the
    /// order somebody arranged them in on screen, so it is what the shelf keeps - see
    /// <see cref="InventoryItem.Position"/>.
    ///
    /// Also raises the standing "keep your stock updated" reminder from the first item an inventory ever
    /// holds, which is what it did when items were added one at a time.
    /// </summary>
    public async Task SaveAsync(
        Guid inventoryId, IReadOnlyList<InventoryItemInput> items, CancellationToken cancellationToken)
    {
        var existingItems = await inventoryItemRepository.GetAllAsync(inventoryId, cancellationToken);
        var keptItemIds = items.Where(item => item.Id is not null).Select(item => item.Id!.Value).ToHashSet();

        foreach (var removed in existingItems.Where(item => !keptItemIds.Contains(item.Id)))
        {
            await inventoryItemRepository.DeleteAsync(inventoryId, removed.Id, cancellationToken);
        }

        foreach (var (input, position) in items.Select((input, position) => (input, position)))
        {
            var existing = input.Id is { } id ? existingItems.FirstOrDefault(item => item.Id == id) : null;
            if (existing is null)
            {
                await AddAsync(inventoryId, input, position, cancellationToken);
                continue;
            }

            existing.Update(
                input.Name, input.ProductType, input.Category, input.Quantity, input.MinimumQuantity,
                input.Unit, input.ExpiryDate, input.ExpiryNotificationChannel,
                input.IsCheckedRegularly ?? existing.IsCheckedRegularly);
            existing.MoveTo(position);
            await SaveWithRestockTaskAsync(existing, cancellationToken);
        }

        if (items.Count > 0)
        {
            await taskListCoordinator.EnsureManagedTaskListAsync(inventoryId, cancellationToken);
        }
    }

    /// <summary>
    /// Leaves the shelf holding nothing. What a private inventory keeps is inside its sealed payload, so
    /// no item row should exist for one at all - including any left over from before it was made private.
    /// Dropping them is what makes "the server can't read this inventory" true rather than aspirational.
    /// </summary>
    public async Task RemoveEverythingAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        foreach (var item in await inventoryItemRepository.GetAllAsync(inventoryId, cancellationToken))
        {
            await inventoryItemRepository.DeleteAsync(inventoryId, item.Id, cancellationToken);
        }
    }

    private async Task AddAsync(
        Guid inventoryId, InventoryItemInput input, int position, CancellationToken cancellationToken)
    {
        var item = InventoryItem.Create(
            inventoryId, input.Name, input.ProductType, input.Category, input.Quantity, input.MinimumQuantity,
            input.Unit, input.ExpiryDate, input.ExpiryNotificationChannel, position,
            input.IsCheckedRegularly ?? false);
        await inventoryItemRepository.AddAsync(item, cancellationToken);
        await SaveWithRestockTaskAsync(item, cancellationToken);
    }

    /// <summary>
    /// Raises a restock task for an item that just went low, or clears a now-irrelevant reference for one
    /// that recovered - the same rule the per-item handlers applied before editing became a bulk save.
    /// </summary>
    private async Task SaveWithRestockTaskAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        item = await taskListCoordinator.EnsureRestockTaskAsync(item, cancellationToken);
        if (!item.IsBelowMinimum)
        {
            item.ClearPendingRestockTask();
        }

        await inventoryItemRepository.UpdateAsync(item, cancellationToken);
    }
}

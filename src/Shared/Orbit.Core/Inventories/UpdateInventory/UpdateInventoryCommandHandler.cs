using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.UpdateInventory;

public sealed class UpdateInventoryCommandHandler : IRequestHandler<UpdateInventoryCommand, EditOutcome>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly InventoryTaskListCoordinator _taskListCoordinator;

    public UpdateInventoryCommandHandler(
        InventoryAccessResolver inventoryAccessResolver, IInventoryRepository inventoryRepository,
        IInventoryItemRepository inventoryItemRepository, InventoryTaskListCoordinator taskListCoordinator)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _taskListCoordinator = taskListCoordinator;
    }

    /// <summary>
    /// Mirrors UpdateTaskListCommandHandler: a read-only grantee gets NotFound, and someone else holding
    /// the edit lock gets Locked. Items are reconciled rather than replaced wholesale - an inventory item
    /// carries state the editor never sees (its open restock task), so a delete-and-reinsert would drop
    /// that and re-raise a restock task the user already has.
    /// </summary>
    public async Task<EditOutcome> HandleAsync(UpdateInventoryCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryAccessResolver.ResolveAsync(request.UserId, request.InventoryId, cancellationToken);
        if (inventory is null)
        {
            return EditOutcome.NotFound;
        }

        // Visible but not theirs to change - see EditOutcomeKind.ReadOnly for why that is worth saying.
        if (!inventory.AccessLevel.AllowsEditing())
        {
            return EditOutcome.ReadOnly;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (inventory.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(inventory.LockedByUserName!);
        }

        // Said nothing about the description, keep the stored one - see UpdateInventoryCommand.
        inventory.Update(
            request.Name, request.IsPrivate, request.EncryptedContent,
            request.Description ?? inventory.Description);
        await _inventoryRepository.UpdateAsync(inventory, cancellationToken);

        if (inventory.IsPrivate)
        {
            // Everything the caller sent is already inside the sealed payload, so no item row should
            // exist here at all - including any left over from before it was made private. Dropping
            // them is what makes "the server can't read this inventory" true rather than aspirational,
            // and it is why a private inventory gets no restock tasks or expiry reminders: both are
            // worked out from rows that are now gone.
            await RemoveEveryItemAsync(request.InventoryId, cancellationToken);
            return EditOutcome.Success;
        }

        await SaveItemsAsync(request, cancellationToken);

        // The standing "keep your stock updated" reminder should exist from the first item this
        // inventory ever holds, exactly as it did when items were added one at a time.
        if (request.Items.Count > 0)
        {
            await _taskListCoordinator.EnsureManagedTaskListAsync(request.InventoryId, cancellationToken);
        }

        return EditOutcome.Success;
    }

    private async Task RemoveEveryItemAsync(Guid inventoryId, CancellationToken cancellationToken)
    {
        foreach (var item in await _inventoryItemRepository.GetAllAsync(inventoryId, cancellationToken))
        {
            await _inventoryItemRepository.DeleteAsync(inventoryId, item.Id, cancellationToken);
        }
    }

    private async Task SaveItemsAsync(UpdateInventoryCommand request, CancellationToken cancellationToken)
    {
        var existingItems = await _inventoryItemRepository.GetAllAsync(request.InventoryId, cancellationToken);
        var keptItemIds = request.Items.Where(item => item.Id is not null).Select(item => item.Id!.Value).ToHashSet();

        foreach (var removed in existingItems.Where(item => !keptItemIds.Contains(item.Id)))
        {
            await _inventoryItemRepository.DeleteAsync(request.InventoryId, removed.Id, cancellationToken);
        }

        // The order the rows arrive in is the order somebody arranged them in on screen, so it is what
        // the shelf keeps - see InventoryItem.Position.
        foreach (var (input, position) in request.Items.Select((input, position) => (input, position)))
        {
            var existing = input.Id is { } id ? existingItems.FirstOrDefault(item => item.Id == id) : null;
            if (existing is null)
            {
                await AddItemAsync(request.InventoryId, input, position, cancellationToken);
                continue;
            }

            existing.Update(
                input.Name, input.ProductType, input.Category, input.Quantity, input.MinimumQuantity,
                input.Unit, input.ExpiryDate, input.ExpiryNotificationChannel,
                input.IsCheckedRegularly ?? existing.IsCheckedRegularly);
            existing.MoveTo(position);
            await SaveWithRestockTaskAsync(existing, cancellationToken);
        }
    }

    private async Task AddItemAsync(
        Guid inventoryId, InventoryItemInput input, int position, CancellationToken cancellationToken)
    {
        var item = InventoryItem.Create(
            inventoryId, input.Name, input.ProductType, input.Category, input.Quantity, input.MinimumQuantity,
            input.Unit, input.ExpiryDate, input.ExpiryNotificationChannel, position,
            input.IsCheckedRegularly ?? false);
        await _inventoryItemRepository.AddAsync(item, cancellationToken);
        await SaveWithRestockTaskAsync(item, cancellationToken);
    }

    /// <summary>
    /// Raises a restock task for an item that just went low, or clears a now-irrelevant reference for one
    /// that recovered - the same rule the per-item handlers applied before editing moved into this bulk save.
    /// </summary>
    private async Task SaveWithRestockTaskAsync(InventoryItem item, CancellationToken cancellationToken)
    {
        item = await _taskListCoordinator.EnsureRestockTaskAsync(item, cancellationToken);
        if (!item.IsBelowMinimum)
        {
            item.ClearPendingRestockTask();
        }

        await _inventoryItemRepository.UpdateAsync(item, cancellationToken);
    }
}

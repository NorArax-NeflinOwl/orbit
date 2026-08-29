using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.UpdateWarehouse;

public sealed class UpdateWarehouseCommandHandler : IRequestHandler<UpdateWarehouseCommand, EditOutcome>
{
    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly InventoryTaskListCoordinator _taskListCoordinator;

    public UpdateWarehouseCommandHandler(
        WarehouseAccessResolver warehouseAccessResolver, IWarehouseRepository warehouseRepository,
        IInventoryRepository inventoryRepository, InventoryTaskListCoordinator taskListCoordinator)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
        _warehouseRepository = warehouseRepository;
        _inventoryRepository = inventoryRepository;
        _taskListCoordinator = taskListCoordinator;
    }

    /// <summary>
    /// Mirrors UpdateTaskListCommandHandler: a read-only grantee gets NotFound, and someone else holding
    /// the edit lock gets Locked. Items are reconciled rather than replaced wholesale - an inventory item
    /// carries state the editor never sees (its open restock task), so a delete-and-reinsert would drop
    /// that and re-raise a restock task the user already has.
    /// </summary>
    public async Task<EditOutcome> HandleAsync(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseAccessResolver.ResolveAsync(request.UserId, request.WarehouseId, cancellationToken);
        if (warehouse is null)
        {
            return EditOutcome.NotFound;
        }

        // Visible but not theirs to change - see EditOutcomeKind.ReadOnly for why that is worth saying.
        if (!warehouse.AccessLevel.AllowsEditing())
        {
            return EditOutcome.ReadOnly;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (warehouse.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(warehouse.LockedByUserName!);
        }

        warehouse.Update(request.Name, request.IsPrivate, request.EncryptedContent);
        await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);

        if (warehouse.IsPrivate)
        {
            // Everything the caller sent is already inside the sealed payload, so no item row should
            // exist here at all - including any left over from before it was made private. Dropping
            // them is what makes "the server can't read this warehouse" true rather than aspirational,
            // and it is why a private warehouse gets no restock tasks or expiry reminders: both are
            // worked out from rows that are now gone.
            await RemoveEveryItemAsync(request.WarehouseId, cancellationToken);
            return EditOutcome.Success;
        }

        await SaveItemsAsync(request, cancellationToken);

        // The standing "keep your stock updated" reminder should exist from the first item this
        // warehouse ever holds, exactly as it did when items were added one at a time.
        if (request.Items.Count > 0)
        {
            await _taskListCoordinator.EnsureManagedTaskListAsync(request.WarehouseId, cancellationToken);
        }

        return EditOutcome.Success;
    }

    private async Task RemoveEveryItemAsync(Guid warehouseId, CancellationToken cancellationToken)
    {
        foreach (var item in await _inventoryRepository.GetAllAsync(warehouseId, cancellationToken))
        {
            await _inventoryRepository.DeleteAsync(warehouseId, item.Id, cancellationToken);
        }
    }

    private async Task SaveItemsAsync(UpdateWarehouseCommand request, CancellationToken cancellationToken)
    {
        var existingItems = await _inventoryRepository.GetAllAsync(request.WarehouseId, cancellationToken);
        var keptItemIds = request.Items.Where(item => item.Id is not null).Select(item => item.Id!.Value).ToHashSet();

        foreach (var removed in existingItems.Where(item => !keptItemIds.Contains(item.Id)))
        {
            await _inventoryRepository.DeleteAsync(request.WarehouseId, removed.Id, cancellationToken);
        }

        // The order the rows arrive in is the order somebody arranged them in on screen, so it is what
        // the shelf keeps - see InventoryItem.Position.
        foreach (var (input, position) in request.Items.Select((input, position) => (input, position)))
        {
            var existing = input.Id is { } id ? existingItems.FirstOrDefault(item => item.Id == id) : null;
            if (existing is null)
            {
                await AddItemAsync(request.WarehouseId, input, position, cancellationToken);
                continue;
            }

            existing.Update(
                input.Name, input.ProductType, input.Category, input.Quantity, input.MinimumQuantity,
                input.Unit, input.ExpiryDate, input.ExpiryNotificationChannel);
            existing.MoveTo(position);
            await SaveWithRestockTaskAsync(existing, cancellationToken);
        }
    }

    private async Task AddItemAsync(
        Guid warehouseId, WarehouseItemInput input, int position, CancellationToken cancellationToken)
    {
        var item = InventoryItem.Create(
            warehouseId, input.Name, input.ProductType, input.Category, input.Quantity, input.MinimumQuantity,
            input.Unit, input.ExpiryDate, input.ExpiryNotificationChannel, position);
        await _inventoryRepository.AddAsync(item, cancellationToken);
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

        await _inventoryRepository.UpdateAsync(item, cancellationToken);
    }
}

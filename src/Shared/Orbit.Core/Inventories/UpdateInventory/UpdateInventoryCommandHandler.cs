using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.UpdateInventory;

public sealed class UpdateInventoryCommandHandler : IRequestHandler<UpdateInventoryCommand, EditOutcome>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly InventoryItemsSaver _itemsSaver;

    public UpdateInventoryCommandHandler(
        InventoryAccessResolver inventoryAccessResolver, IInventoryRepository inventoryRepository,
        InventoryItemsSaver itemsSaver)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _inventoryRepository = inventoryRepository;
        _itemsSaver = itemsSaver;
    }

    /// <summary>
    /// Mirrors UpdateTaskListCommandHandler: a read-only grantee gets NotFound, and someone else holding
    /// the edit lock gets Locked. The items themselves are written by <see cref="InventoryItemsSaver"/>,
    /// which creating an inventory now uses too.
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
            await _itemsSaver.RemoveEverythingAsync(request.InventoryId, cancellationToken);
            return EditOutcome.Success;
        }

        await _itemsSaver.SaveAsync(request.InventoryId, request.Items, cancellationToken);
        return EditOutcome.Success;
    }
}

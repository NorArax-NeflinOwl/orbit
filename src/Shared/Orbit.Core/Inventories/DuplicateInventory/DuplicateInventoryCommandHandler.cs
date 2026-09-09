using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.DuplicateInventory;

public sealed class DuplicateInventoryCommandHandler : IRequestHandler<DuplicateInventoryCommand, Guid?>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IInventoryItemRepository _inventoryItemRepository;
    private readonly InventoryItemsSaver _itemsSaver;

    public DuplicateInventoryCommandHandler(
        IInventoryRepository inventoryRepository, IInventoryItemRepository inventoryItemRepository,
        InventoryItemsSaver itemsSaver)
    {
        _inventoryRepository = inventoryRepository;
        _inventoryItemRepository = inventoryItemRepository;
        _itemsSaver = itemsSaver;
    }

    /// <summary>
    /// The storage and everything on its shelves, filled through the same
    /// <see cref="InventoryItemsSaver"/> a save uses - so the rows land with their positions and their
    /// restock tasks exactly as they would on the next save rather than nearly.
    ///
    /// The rows are copied as new items: each is sent with a null id, which is what tells the saver
    /// "create this" rather than "update that" (see InventoryItemInput.Id). An item's open restock
    /// errand is therefore not copied, and should not be - the errand is about the shelf it was raised
    /// from, and two shelves claiming one errand is the state PendingRestockTaskResolver exists to
    /// prevent.
    ///
    /// A private inventory keeps no readable rows at all - what it holds is sealed inside its payload -
    /// so there is nothing to copy beside it, and the copy opens with the same key, having the same owner.
    /// </summary>
    public async Task<Guid?> HandleAsync(DuplicateInventoryCommand request, CancellationToken cancellationToken)
    {
        if (await _inventoryRepository.GetByIdAsync(request.UserId, request.Id, cancellationToken) is not { } inventory)
        {
            return null;
        }

        var copy = Inventory.Create(
            inventory.UserId,
            // A sealed inventory's name is inside the payload, so there is nothing here to rename.
            inventory.IsPrivate ? inventory.Name : request.Name ?? inventory.Name,
            inventory.IsPrivate,
            inventory.EncryptedContent,
            inventory.Description);
        await _inventoryRepository.AddAsync(copy, cancellationToken);

        if (copy.IsPrivate)
        {
            return copy.Id;
        }

        var items = await _inventoryItemRepository.GetAllAsync(inventory.Id, cancellationToken);
        if (items.Count > 0)
        {
            await _itemsSaver.SaveAsync(copy.Id, [.. items.Select(CopyOf)], cancellationToken);
        }

        return copy.Id;
    }

    private static InventoryItemInput CopyOf(InventoryItem item)
        => new(
            Id: null, item.Name, item.ProductType, item.Categories, item.Quantity, item.MinimumQuantity,
            item.Unit, item.ExpiryDate, item.ExpiryNotificationChannel, item.IsCheckedRegularly);
}

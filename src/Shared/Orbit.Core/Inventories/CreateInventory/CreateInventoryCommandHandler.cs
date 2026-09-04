using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.CreateInventory;

public sealed class CreateInventoryCommandHandler : IRequestHandler<CreateInventoryCommand, Guid>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly InventoryItemsSaver _itemsSaver;

    public CreateInventoryCommandHandler(IInventoryRepository inventoryRepository, InventoryItemsSaver itemsSaver)
    {
        _inventoryRepository = inventoryRepository;
        _itemsSaver = itemsSaver;
    }

    /// <summary>
    /// Creates the inventory and, when the caller sent any, fills it - through the same
    /// <see cref="InventoryItemsSaver"/> a save uses, so the rows get their positions and their restock
    /// tasks exactly as they would have on the next save rather than nearly.
    ///
    /// A private inventory keeps no readable rows at all: what it holds is sealed inside its payload,
    /// and writing item rows beside that would make "the server can't read this inventory" false. The
    /// browser already sends an empty list for one (see InventoryApiClient.SealIfPrivateAsync); this is
    /// what makes that a rule rather than a client's good manners.
    /// </summary>
    public async Task<Guid> HandleAsync(CreateInventoryCommand request, CancellationToken cancellationToken)
    {
        var inventory = Inventory.Create(
            request.UserId, request.Name, request.IsPrivate, request.EncryptedContent,
            request.Description ?? string.Empty);
        await _inventoryRepository.AddAsync(inventory, cancellationToken);

        if (!inventory.IsPrivate && request.Items is { Count: > 0 } items)
        {
            await _itemsSaver.SaveAsync(inventory.Id, items, cancellationToken);
        }

        return inventory.Id;
    }
}

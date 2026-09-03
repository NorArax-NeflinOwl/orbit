using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.CreateInventory;

public sealed class CreateInventoryCommandHandler : IRequestHandler<CreateInventoryCommand, Guid>
{
    private readonly IInventoryRepository _inventoryRepository;

    public CreateInventoryCommandHandler(IInventoryRepository inventoryRepository)
    {
        _inventoryRepository = inventoryRepository;
    }

    public async Task<Guid> HandleAsync(CreateInventoryCommand request, CancellationToken cancellationToken)
    {
        var inventory = Inventory.Create(
            request.UserId, request.Name, request.IsPrivate, request.EncryptedContent,
            request.Description ?? string.Empty);
        await _inventoryRepository.AddAsync(inventory, cancellationToken);
        return inventory.Id;
    }
}

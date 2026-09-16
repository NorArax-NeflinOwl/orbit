using Orbit.Core.Abstractions;
using Orbit.Core.Folders;

namespace Orbit.Core.Inventories.MoveInventoryToFolder;

/// <summary>
/// Only the inventory's owner files it, and only into a folder of their own - see
/// MoveNoteToFolderCommandHandler, which makes the same two checks for the same reasons. A private
/// inventory is filed like any other: its folder is outside the sealed half, so the server can move it
/// without a key (see Inventory.FolderId).
/// </summary>
public sealed class MoveInventoryToFolderCommandHandler : IRequestHandler<MoveInventoryToFolderCommand, bool>
{
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IFolderRepository _folderRepository;

    public MoveInventoryToFolderCommandHandler(
        IInventoryRepository inventoryRepository, IFolderRepository folderRepository)
    {
        _inventoryRepository = inventoryRepository;
        _folderRepository = folderRepository;
    }

    public async Task<bool> HandleAsync(MoveInventoryToFolderCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryRepository.GetByIdAsync(request.UserId, request.InventoryId, cancellationToken);
        if (inventory is null || inventory.UserId != request.UserId)
        {
            return false;
        }

        if (request.FolderId is { } folderId
            && await _folderRepository.GetByIdAsync(request.UserId, folderId, cancellationToken) is null)
        {
            return false;
        }

        inventory.MoveToFolder(request.FolderId);
        await _inventoryRepository.UpdateAsync(inventory, cancellationToken);
        return true;
    }
}

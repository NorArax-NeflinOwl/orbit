using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.ArchiveInventory;

/// <summary>
/// Only the owner puts a shelf away, and only their own. A recipient must not: one row is one shelf, so
/// archiving one shared with them would take it off its owner's page - the same reason they cannot file
/// or pin one. Mirrors MoveInventoryToFolderCommandHandler on who may.
/// </summary>
public sealed class ArchiveInventoryCommandHandler : IRequestHandler<ArchiveInventoryCommand, bool>
{
    private readonly IInventoryRepository _inventories;

    public ArchiveInventoryCommandHandler(IInventoryRepository inventories) => _inventories = inventories;

    public async Task<bool> HandleAsync(ArchiveInventoryCommand request, CancellationToken cancellationToken)
    {
        var found = await _inventories.GetByIdAsync(request.UserId, request.InventoryId, cancellationToken);
        if (found is null || found.UserId != request.UserId)
        {
            return false;
        }

        found.Archive(request.IsArchived);
        await _inventories.UpdateAsync(found, cancellationToken);
        return true;
    }
}

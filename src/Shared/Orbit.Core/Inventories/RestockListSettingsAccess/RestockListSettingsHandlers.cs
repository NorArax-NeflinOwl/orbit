using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.RestockListSettingsAccess;

/// <summary>
/// All three go through <see cref="InventoryAccessResolver"/> first: the restock list belongs to a
/// inventory, so who may change how it behaves is who may change the inventory.
/// </summary>
public sealed class GetRestockListSettingsQueryHandler : IRequestHandler<GetRestockListSettingsQuery, RestockListSettings?>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly IInventoryManagedTaskListRepository _managedTaskListRepository;

    public GetRestockListSettingsQueryHandler(
        InventoryAccessResolver inventoryAccessResolver, IInventoryManagedTaskListRepository managedTaskListRepository)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _managedTaskListRepository = managedTaskListRepository;
    }

    public async Task<RestockListSettings?> HandleAsync(
        GetRestockListSettingsQuery request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryAccessResolver.ResolveAsync(request.UserId, request.InventoryId, cancellationToken);
        return inventory is null
            ? null
            : await _managedTaskListRepository.GetSettingsAsync(request.InventoryId, cancellationToken);
    }
}

public sealed class SaveRestockListSettingsCommandHandler
    : IRequestHandler<SaveRestockListSettingsCommand, RestockRefreshOutcome>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly IInventoryManagedTaskListRepository _managedTaskListRepository;
    private readonly RestockListRefresh _restockListRefresh;

    public SaveRestockListSettingsCommandHandler(
        InventoryAccessResolver inventoryAccessResolver, IInventoryManagedTaskListRepository managedTaskListRepository,
        RestockListRefresh restockListRefresh)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _managedTaskListRepository = managedTaskListRepository;
        _restockListRefresh = restockListRefresh;
    }

    public async Task<RestockRefreshOutcome> HandleAsync(
        SaveRestockListSettingsCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryAccessResolver.ResolveAsync(request.UserId, request.InventoryId, cancellationToken);
        if (inventory is null || !inventory.AccessLevel.AllowsEditing())
        {
            return RestockRefreshOutcome.Nothing;
        }

        await _managedTaskListRepository.SetSettingsAsync(request.InventoryId, request.Settings, cancellationToken);
        return await _restockListRefresh.RefreshAsync(request.InventoryId, cancellationToken);
    }
}

public sealed class RefreshRestockListCommandHandler : IRequestHandler<RefreshRestockListCommand, RestockRefreshOutcome>
{
    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly RestockListRefresh _restockListRefresh;

    public RefreshRestockListCommandHandler(
        InventoryAccessResolver inventoryAccessResolver, RestockListRefresh restockListRefresh)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _restockListRefresh = restockListRefresh;
    }

    public async Task<RestockRefreshOutcome> HandleAsync(
        RefreshRestockListCommand request, CancellationToken cancellationToken)
    {
        var inventory = await _inventoryAccessResolver.ResolveAsync(request.UserId, request.InventoryId, cancellationToken);
        return inventory is null || !inventory.AccessLevel.AllowsEditing()
            ? RestockRefreshOutcome.Nothing
            : await _restockListRefresh.RefreshAsync(request.InventoryId, cancellationToken);
    }
}

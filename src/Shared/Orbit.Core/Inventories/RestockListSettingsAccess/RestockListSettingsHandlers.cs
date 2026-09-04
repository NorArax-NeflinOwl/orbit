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
    private readonly InventoryTaskListCoordinator _taskListCoordinator;

    public SaveRestockListSettingsCommandHandler(
        InventoryAccessResolver inventoryAccessResolver, IInventoryManagedTaskListRepository managedTaskListRepository,
        RestockListRefresh restockListRefresh, InventoryTaskListCoordinator taskListCoordinator)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _managedTaskListRepository = managedTaskListRepository;
        _restockListRefresh = restockListRefresh;
        _taskListCoordinator = taskListCoordinator;
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

        if (!request.Settings.IsEnabled)
        {
            // Off takes the list with it - see RestockListSettings.IsEnabled. Written unconditionally
            // rather than only on the off-to-on edge: a list that exists while the setting says it
            // should not is the state to correct, however it came about. Nothing to refresh afterwards,
            // since RefreshAsync would only build a fresh one.
            await _taskListCoordinator.DeleteManagedTaskListAsync(request.InventoryId, cancellationToken);
            return RestockRefreshOutcome.Nothing;
        }

        // On: refresh ensures the list first, so this covers both reconciling one that exists and
        // building the fresh one that switching back on asks for.
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

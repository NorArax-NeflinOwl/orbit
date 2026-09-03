using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.RestockListSettingsAccess;

/// <summary>
/// All three go through <see cref="WarehouseAccessResolver"/> first: the restock list belongs to a
/// warehouse, so who may change how it behaves is who may change the warehouse.
/// </summary>
public sealed class GetRestockListSettingsQueryHandler : IRequestHandler<GetRestockListSettingsQuery, RestockListSettings?>
{
    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly IInventoryManagedTaskListRepository _managedTaskListRepository;

    public GetRestockListSettingsQueryHandler(
        WarehouseAccessResolver warehouseAccessResolver, IInventoryManagedTaskListRepository managedTaskListRepository)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
        _managedTaskListRepository = managedTaskListRepository;
    }

    public async Task<RestockListSettings?> HandleAsync(
        GetRestockListSettingsQuery request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseAccessResolver.ResolveAsync(request.UserId, request.WarehouseId, cancellationToken);
        return warehouse is null
            ? null
            : await _managedTaskListRepository.GetSettingsAsync(request.WarehouseId, cancellationToken);
    }
}

public sealed class SaveRestockListSettingsCommandHandler
    : IRequestHandler<SaveRestockListSettingsCommand, RestockRefreshOutcome>
{
    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly IInventoryManagedTaskListRepository _managedTaskListRepository;
    private readonly RestockListRefresh _restockListRefresh;

    public SaveRestockListSettingsCommandHandler(
        WarehouseAccessResolver warehouseAccessResolver, IInventoryManagedTaskListRepository managedTaskListRepository,
        RestockListRefresh restockListRefresh)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
        _managedTaskListRepository = managedTaskListRepository;
        _restockListRefresh = restockListRefresh;
    }

    public async Task<RestockRefreshOutcome> HandleAsync(
        SaveRestockListSettingsCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseAccessResolver.ResolveAsync(request.UserId, request.WarehouseId, cancellationToken);
        if (warehouse is null || !warehouse.AccessLevel.AllowsEditing())
        {
            return RestockRefreshOutcome.Nothing;
        }

        await _managedTaskListRepository.SetSettingsAsync(request.WarehouseId, request.Settings, cancellationToken);
        return await _restockListRefresh.RefreshAsync(request.WarehouseId, cancellationToken);
    }
}

public sealed class RefreshRestockListCommandHandler : IRequestHandler<RefreshRestockListCommand, RestockRefreshOutcome>
{
    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly RestockListRefresh _restockListRefresh;

    public RefreshRestockListCommandHandler(
        WarehouseAccessResolver warehouseAccessResolver, RestockListRefresh restockListRefresh)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
        _restockListRefresh = restockListRefresh;
    }

    public async Task<RestockRefreshOutcome> HandleAsync(
        RefreshRestockListCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseAccessResolver.ResolveAsync(request.UserId, request.WarehouseId, cancellationToken);
        return warehouse is null || !warehouse.AccessLevel.AllowsEditing()
            ? RestockRefreshOutcome.Nothing
            : await _restockListRefresh.RefreshAsync(request.WarehouseId, cancellationToken);
    }
}

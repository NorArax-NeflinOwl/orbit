using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Inventory.AcquireWarehouseLock;

/// <summary>Mirrors Orbit.Core.Tasks.AcquireTaskListLock.AcquireTaskListLockCommandHandler - see its comment.</summary>
public sealed class AcquireWarehouseLockCommandHandler : IRequestHandler<AcquireWarehouseLockCommand, EditOutcome>
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(60);

    private readonly WarehouseAccessResolver _warehouseAccessResolver;
    private readonly IWarehouseRepository _warehouseRepository;
    private readonly IUserRepository _userRepository;

    public AcquireWarehouseLockCommandHandler(
        WarehouseAccessResolver warehouseAccessResolver, IWarehouseRepository warehouseRepository, IUserRepository userRepository)
    {
        _warehouseAccessResolver = warehouseAccessResolver;
        _warehouseRepository = warehouseRepository;
        _userRepository = userRepository;
    }

    public async Task<EditOutcome> HandleAsync(AcquireWarehouseLockCommand request, CancellationToken cancellationToken)
    {
        var warehouse = await _warehouseAccessResolver.ResolveAsync(request.UserId, request.WarehouseId, cancellationToken);
        if (warehouse is null || warehouse.AccessLevel != ShareAccessLevel.CanEdit)
        {
            return EditOutcome.NotFound;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        if (warehouse.IsLockedByAnotherUser(request.UserId, nowUtc))
        {
            return EditOutcome.LockedBy(warehouse.LockedByUserName!);
        }

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        warehouse.AcquireLock(request.UserId, user!.UserName, nowUtc, LockDuration);
        await _warehouseRepository.UpdateAsync(warehouse, cancellationToken);
        return EditOutcome.Success;
    }
}

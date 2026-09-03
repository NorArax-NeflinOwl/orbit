using Orbit.Core.Abstractions;
using Orbit.Core.Users;

namespace Orbit.Core.Inventories.AcquireInventoryLock;

/// <summary>Mirrors Orbit.Core.Tasks.AcquireTaskListLock.AcquireTaskListLockCommandHandler - see its comment.</summary>
public sealed class AcquireInventoryLockCommandHandler : IRequestHandler<AcquireInventoryLockCommand, EditOutcome>
{
    private static readonly TimeSpan LockDuration = TimeSpan.FromSeconds(60);

    private readonly InventoryAccessResolver _inventoryAccessResolver;
    private readonly IInventoryRepository _inventoryRepository;
    private readonly IUserRepository _userRepository;

    public AcquireInventoryLockCommandHandler(
        InventoryAccessResolver inventoryAccessResolver, IInventoryRepository inventoryRepository, IUserRepository userRepository)
    {
        _inventoryAccessResolver = inventoryAccessResolver;
        _inventoryRepository = inventoryRepository;
        _userRepository = userRepository;
    }

    public async Task<EditOutcome> HandleAsync(AcquireInventoryLockCommand request, CancellationToken cancellationToken)
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

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        inventory.AcquireLock(request.UserId, user!.UserName, nowUtc, LockDuration);
        await _inventoryRepository.UpdateLockAsync(inventory, cancellationToken);
        return EditOutcome.Success;
    }
}

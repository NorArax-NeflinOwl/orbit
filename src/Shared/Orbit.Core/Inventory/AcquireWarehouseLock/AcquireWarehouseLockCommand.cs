using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.AcquireWarehouseLock;

/// <summary>Mirrors Orbit.Core.Tasks.AcquireTaskListLock.AcquireTaskListLockCommand - see its comment.</summary>
public sealed record AcquireWarehouseLockCommand(Guid UserId, Guid WarehouseId) : IRequest<EditOutcome>;

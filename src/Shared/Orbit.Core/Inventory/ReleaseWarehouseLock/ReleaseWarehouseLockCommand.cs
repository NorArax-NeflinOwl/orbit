using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventory.ReleaseWarehouseLock;

/// <summary>Mirrors Orbit.Core.Tasks.ReleaseTaskListLock.ReleaseTaskListLockCommand - see its comment.</summary>
public sealed record ReleaseWarehouseLockCommand(Guid UserId, Guid WarehouseId) : IRequest<bool>;

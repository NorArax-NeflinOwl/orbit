using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.AcquireInventoryLock;

/// <summary>Mirrors Orbit.Core.Tasks.AcquireTaskListLock.AcquireTaskListLockCommand - see its comment.</summary>
public sealed record AcquireInventoryLockCommand(Guid UserId, Guid InventoryId) : IRequest<EditOutcome>;

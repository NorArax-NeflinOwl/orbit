using Orbit.Core.Abstractions;

namespace Orbit.Core.Inventories.ReleaseInventoryLock;

/// <summary>Mirrors Orbit.Core.Tasks.ReleaseTaskListLock.ReleaseTaskListLockCommand - see its comment.</summary>
public sealed record ReleaseInventoryLockCommand(Guid UserId, Guid InventoryId) : IRequest<bool>;

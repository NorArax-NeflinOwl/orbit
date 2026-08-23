using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.AcquireTaskListLock;

/// <summary>Mirrors Orbit.Core.Notes.AcquireNoteLock.AcquireNoteLockCommand - see its comment.</summary>
public sealed record AcquireTaskListLockCommand(Guid UserId, Guid TaskListId) : IRequest<EditOutcome>;

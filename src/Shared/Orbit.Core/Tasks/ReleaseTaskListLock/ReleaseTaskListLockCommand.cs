using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks.ReleaseTaskListLock;

/// <summary>Mirrors Orbit.Core.Notes.ReleaseNoteLock.ReleaseNoteLockCommand - see its comment.</summary>
public sealed record ReleaseTaskListLockCommand(Guid UserId, Guid TaskListId) : IRequest<bool>;

namespace Orbit.Core.Tasks;

public interface ITaskListShareRepository
{
    Task AddAsync(TaskListShare share, CancellationToken cancellationToken);

    /// <summary>
    /// Scoped to recipientUserId, the same way ITaskRepository.GetByIdAsync is scoped to an owner -
    /// returns null both when the share doesn't exist and when it exists but was offered to someone
    /// else, so a caller can't tell one case from the other by probing ids.
    /// </summary>
    Task<TaskListShare?> GetByIdAsync(Guid recipientUserId, Guid id, CancellationToken cancellationToken);

    Task UpdateAsync(TaskListShare share, CancellationToken cancellationToken);

    /// <summary>
    /// The share already offered for sourceTaskListId to recipientUserId, if one exists - accepted or
    /// still pending, either way counts as "already shared" for ShareTaskListCommandHandler's duplicate
    /// check, so it re-sends the existing offer as a reminder instead of creating a second one.
    /// </summary>
    Task<TaskListShare?> FindExistingAsync(Guid sourceTaskListId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>The *accepted* grant for sourceTaskListId to recipientUserId, if one exists - see TaskListAccessResolver.</summary>
    Task<TaskListShare?> FindAcceptedGrantAsync(Guid sourceTaskListId, Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>Every task list recipientUserId has accepted access to, regardless of which owner shared it - see TaskListAccessResolver.ResolveAllAsync.</summary>
    Task<IReadOnlyList<TaskListShare>> GetAcceptedGrantsForRecipientAsync(Guid recipientUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of ownerUserId's own task lists somebody else currently holds accepted access to - the owner's
    /// side of the relationship, which nothing else exposes. Mirrors INoteShareRepository's method of the
    /// same shape, and exists for the same reason: a mobile client cannot hold an edit lock, so anything
    /// another person can change is read-only while offline (info/orbit-maui-plan.md §5.4).
    ///
    /// A whole set in one query, because the caller asks it of every task list in a list.
    /// </summary>
    Task<IReadOnlySet<Guid>> GetSharedOutTaskListIdsAsync(Guid ownerUserId, CancellationToken cancellationToken);

    /// <summary>
    /// Drops the accepted grant that puts this task list on recipientUserId's list, taking it off their
    /// list without touching the owner's. Scoped to the recipient, so it can only ever remove their own
    /// access. A no-op when there is no such grant.
    /// </summary>
    Task RemoveAcceptedGrantAsync(Guid sourceId, Guid recipientUserId, CancellationToken cancellationToken);
}

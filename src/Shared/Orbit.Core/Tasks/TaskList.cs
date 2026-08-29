using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks;

/// <summary>
/// A titled checklist, owned by exactly one user (<see cref="UserId"/>) for its entire lifetime - mirrors
/// Orbit.Core.Notes.Note, see its class comment for why IsShared/SharedByUserName/AccessLevel aren't
/// persisted. Named "TaskList" rather than "Task" to avoid colliding with
/// <see cref="System.Threading.Tasks.Task"/>, which every async method in this codebase returns.
/// </summary>
public sealed class TaskList
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    /// <summary>Empty for a private list - its real title is inside <see cref="EncryptedContent"/>.</summary>
    public string Title { get; private set; }

    /// <summary>Empty for a private list - its real items are inside <see cref="EncryptedContent"/>.</summary>
    public IReadOnlyList<TaskItem> Items { get; private set; }

    /// <summary>
    /// Marks a list only its owner can ever read: its items are sealed in the browser before they get
    /// here, and it can't be shared. Because the server can no longer read a due date or a description,
    /// a private list gets no overdue or daily reminders - see EncryptedPayload's comment.
    /// </summary>
    public bool IsPrivate { get; private set; }

    /// <summary>The sealed title and items of a private list; null for an ordinary one.</summary>
    public EncryptedPayload? EncryptedContent { get; private set; }

    /// <summary>
    /// Derived, not settable directly: a task list is done exactly when every item on it is checked
    /// off, and an empty list is never considered done.
    /// </summary>
    public bool IsCompleted { get; private set; }

    /// <summary>How much this list matters, for sorting and for the reader's own sense of it. See <see cref="ItemPriority"/>.</summary>
    public ItemPriority Priority { get; private set; }

    /// <summary>
    /// Keeps this list at the top of the Tasks page whatever the reader is sorting by. Separate from
    /// Priority on purpose: priority says how much something matters, pinning says "keep this where I
    /// can see it", and the two are not the same wish - a low-priority list can still be the one being
    /// worked on today.
    /// </summary>
    public bool IsPinned { get; private set; }

    /// <summary>
    /// Marks this list as one that gathers other lists: the lists its items link to are its members,
    /// and the checklist view renders them inline underneath it so the whole group can be worked
    /// through in one place. Purely a presentation flag - completion still follows the same rules,
    /// with each linked item resolving to its list's completion (see LinkedTaskCompletionResolver).
    /// </summary>
    public bool IsGroup { get; private set; }

    /// <summary>
    /// The warehouse this list's work is measured against, when one has been chosen - see
    /// StockRequirementCounter. Null for a list nobody has asked that question of.
    /// </summary>
    public Guid? LinkedWarehouseId { get; private set; }

    /// <summary>The user id currently holding the edit lock, if any - see AcquireLock/ReleaseLock.</summary>
    public Guid? LockedByUserId { get; private set; }

    /// <summary>The locking user's login, captured at lock-acquisition time for display - meaningless when LockedByUserId is null.</summary>
    public string? LockedByUserName { get; private set; }

    /// <summary>Once past, the lock is treated as abandoned (e.g. a crashed tab) and anyone can acquire a fresh one.</summary>
    public DateTimeOffset? LockExpiresAtUtc { get; private set; }

    /// <summary>False for the owner, true for anyone viewing/editing this task list through a share - see TaskListAccessResolver.</summary>
    public bool IsShared { get; private set; }

    /// <summary>The owner's login, whenever IsShared is true. Null otherwise.</summary>
    public string? SharedByUserName { get; private set; }

    /// <summary>
    /// True when somebody else holds accepted access to this. Only ever meaningful to the owner - the
    /// recipient's side of the same relationship is <see cref="IsShared"/>. Stamped by the access
    /// resolver rather than stored, because it depends on who is asking. See NoteDto for why a mobile
    /// client needs it.
    /// </summary>
    public bool IsSharedWithOthers { get; private set; }

    /// <summary>The current caller's access level - always CanEdit for the owner, and whatever their share grants otherwise.</summary>
    public ShareAccessLevel AccessLevel { get; private set; } = ShareAccessLevel.CanEdit;

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    private TaskList(
        Guid id, Guid userId, string title, IReadOnlyList<TaskItem> items, bool isGroup, bool isPrivate, EncryptedPayload? encryptedContent,
        ItemPriority priority, bool isPinned, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc)
    {
        Id = id;
        UserId = userId;
        (Title, Items, IsPrivate, EncryptedContent) = ReadableOrSealed(title, items, isPrivate, encryptedContent);
        IsGroup = isGroup;
        IsCompleted = ComputeIsCompleted(Items);
        Priority = priority;
        IsPinned = isPinned;
        LockedByUserId = lockedByUserId;
        LockedByUserName = lockedByUserName;
        LockExpiresAtUtc = lockExpiresAtUtc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static TaskList Create(
        Guid userId, string title, IReadOnlyList<TaskItem> items, bool isGroup = false,
        bool isPrivate = false, EncryptedPayload? encryptedContent = null, ItemPriority priority = ItemPriority.Normal,
        bool isPinned = false)
    {
        EnsureSealedWhenPrivate(isPrivate, encryptedContent);
        var now = DateTimeOffset.UtcNow;
        return new TaskList(
            Guid.NewGuid(), userId, title, items, isGroup, isPrivate, encryptedContent, priority, isPinned, now, now,
            lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null);
    }

    /// <summary>
    /// Rebuilds a task list from already-persisted values, bypassing creation rules. Every parameter is
    /// required on purpose, priority included: this is also used to rebuild a list in memory (see
    /// LinkedTaskCompletionResolver), and an optional parameter there is a field that silently reverts
    /// to its default instead of a compiler error.
    /// </summary>
    public static TaskList FromPersistence(
        Guid id, Guid userId, string title, IReadOnlyList<TaskItem> items, bool isGroup, bool isPrivate, EncryptedPayload? encryptedContent,
        DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc,
        ItemPriority priority, bool isPinned, Guid? linkedWarehouseId = null)
    {
        var taskList = new TaskList(id, userId, title, items, isGroup, isPrivate, encryptedContent, priority, isPinned,
            createdAtUtc, updatedAtUtc, lockedByUserId, lockedByUserName, lockExpiresAtUtc);
        taskList.LinkedWarehouseId = linkedWarehouseId;
        return taskList;
    }

    /// <summary>Stamps how the current caller relates to this task list - see the class comment. Not persisted.</summary>
    /// <summary>Tells the owner that somebody else holds accepted access - the mirror of <see cref="IsShared"/>.</summary>
    public void SetSharedWithOthers(bool isSharedWithOthers) => IsSharedWithOthers = isSharedWithOthers;

    public void SetAccessContext(bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
    {
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
        AccessLevel = accessLevel;
    }

    /// <summary>
    /// Replaces the title, the whole checklist, the grouping flag and the priority, then recomputes
    /// <see cref="IsCompleted"/> from the new items. Callers are expected to have already checked
    /// AccessLevel is CanEdit and the list isn't locked by someone else - see
    /// UpdateTaskListCommandHandler.
    ///
    /// No parameter here has a default, on purpose: this replaces the whole list, so a caller that
    /// forgot one would silently reset it. <paramref name="priority"/> used to default to Normal, and
    /// every caller that appends a single item - a restock errand, a moved entry - forgot it, so a list
    /// somebody had marked High quietly dropped back the next time the warehouse touched it.
    /// </summary>
    public void Update(
        string title, IReadOnlyList<TaskItem> items, bool isGroup, bool isPrivate, EncryptedPayload? encryptedContent,
        ItemPriority priority)
    {
        EnsureSealedWhenPrivate(isPrivate, encryptedContent);
        (Title, Items, IsPrivate, EncryptedContent) = ReadableOrSealed(title, items, isPrivate, encryptedContent);
        IsGroup = isGroup;
        IsCompleted = ComputeIsCompleted(Items);
        Priority = priority;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Crosses off everything still outstanding, and says the list changed - which is what carries the
    /// change to anybody else's copy of it. Answers whether anything actually moved.
    ///
    /// The stamp is the point. Completing the items one by one left UpdatedAtUtc where it was, so the
    /// change feed never mentioned the list again: the browser that did it re-read the list itself and
    /// looked right, while a phone showed the round still outstanding for good - see
    /// FinishRestockingCommandHandler.
    /// </summary>
    public bool CompleteEverything()
    {
        // An entry standing for another list is completed by that list being finished, not by this -
        // see TaskItem.Complete - so what moved is asked of the items rather than assumed.
        var crossedOff = false;
        foreach (var item in Items.Where(item => !item.IsCompleted))
        {
            item.Complete();
            crossedOff |= item.IsCompleted;
        }

        if (!crossedOff)
        {
            return false;
        }

        IsCompleted = ComputeIsCompleted(Items);
        UpdatedAtUtc = DateTimeOffset.UtcNow;
        return true;
    }

    /// <summary>
    /// Pinning is its own action rather than part of Update, so it can be done from the list of lists
    /// without loading, editing and saving the whole thing - and so it never collides with someone
    /// else's edit lock, which is about the content rather than where the card sits.
    /// </summary>
    /// <summary>
    /// Points this list at a warehouse, or at none. Its own command rather than part of an update, for
    /// the same reason pinning is: it changes what the list is measured against, not what is on it.
    /// </summary>
    public void LinkToWarehouse(Guid? warehouseId) => LinkedWarehouseId = warehouseId;

    public void SetPinned(bool isPinned)
    {
        if (IsPinned == isPinned)
        {
            return;
        }

        IsPinned = isPinned;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>Mirrors Note.EnsureSealedWhenPrivate - see its comment for why rebuilding a stored row deliberately skips this.</summary>
    private static void EnsureSealedWhenPrivate(bool isPrivate, EncryptedPayload? encryptedContent)
    {
        if (isPrivate && encryptedContent is null)
        {
            throw new InvalidRequestException("A private task list must arrive already encrypted.");
        }
    }

    /// <summary>
    /// Where this list has got to, worked out from its items - see <see cref="TaskListStatus"/> for why
    /// it isn't stored. A private list always reads as New: its items are sealed, so there is nothing
    /// here to work it out from, and guessing would be worse than saying nothing.
    /// </summary>
    public TaskListStatus Status => ComputeStatus(Items, DateTimeOffset.UtcNow);

    private static TaskListStatus ComputeStatus(IReadOnlyList<TaskItem> items, DateTimeOffset nowUtc)
    {
        if (items.Count == 0)
        {
            return TaskListStatus.New;
        }

        if (items.All(item => item.IsCompleted))
        {
            return TaskListStatus.Completed;
        }

        if (items.Any(item => !item.IsCompleted && item.DueDateUtc is { } dueDateUtc && dueDateUtc < nowUtc))
        {
            return TaskListStatus.Overdue;
        }

        return items.Any(item => item.IsCompleted) ? TaskListStatus.Pending : TaskListStatus.New;
    }

    /// <summary>Mirrors Note.ReadableOrSealed - see its comment for why this is enforced rather than trusted.</summary>
    private static (string Title, IReadOnlyList<TaskItem> Items, bool IsPrivate, EncryptedPayload? EncryptedContent) ReadableOrSealed(
        string title, IReadOnlyList<TaskItem> items, bool isPrivate, EncryptedPayload? encryptedContent)
    {
        if (!isPrivate)
        {
            return (title, items, false, null);
        }

        // No check for a missing payload here - see EnsureSealedWhenPrivate for where that lives and why.
        return (string.Empty, [], true, encryptedContent);
    }

    public bool IsLockedByAnotherUser(Guid callerId, DateTimeOffset nowUtc)
        => LockedByUserId is { } lockedByUserId && lockedByUserId != callerId && LockExpiresAtUtc > nowUtc;

    /// <summary>Mirrors Note.AcquireLock - see its comment.</summary>
    public void AcquireLock(Guid userId, string userName, DateTimeOffset nowUtc, TimeSpan lockDuration)
    {
        LockedByUserId = userId;
        LockedByUserName = userName;
        LockExpiresAtUtc = nowUtc + lockDuration;
    }

    /// <summary>No-op if userId isn't the current lock holder, so releasing an already-expired-and-reassigned lock can't steal it back.</summary>
    public void ReleaseLock(Guid userId)
    {
        if (LockedByUserId != userId)
        {
            return;
        }

        LockedByUserId = null;
        LockedByUserName = null;
        LockExpiresAtUtc = null;
    }

    private static bool ComputeIsCompleted(IReadOnlyList<TaskItem> items)
        => items.Count > 0 && items.All(item => item.IsCompleted);
}

using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks;

/// <summary>
/// An offer to add a copy of a task list to another user's own task lists - created via
/// ShareTaskListCommand, and resolved once the recipient accepts it from the chat message that carries
/// this share's id (see AcceptTaskListShareCommand). Mirrors Orbit.Core.Calendar.CalendarEventShare -
/// see its class comment for the reasoning behind this shape. SourceTaskListId always belongs to OwnerUserId.
/// </summary>
public sealed class TaskListShare
{
    public Guid Id { get; private set; }
    public Guid SourceTaskListId { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public Guid RecipientUserId { get; private set; }
    public ShareAccessLevel AccessLevel { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? AcceptedAtUtc { get; private set; }

    /// <summary>The copy created in the recipient's own task lists - set once accepted, null until then.</summary>
    public Guid? SharedTaskListId { get; private set; }

    /// <summary>
    /// The id of the user who first created the task list being offered, before any sharing - mirrors
    /// <see cref="Orbit.Core.Notes.NoteShare.OriginalOwnerUserId"/>, see its comment.
    /// </summary>
    public Guid OriginalOwnerUserId { get; private set; }

    public bool IsAccepted => AcceptedAtUtc is not null;

    private TaskListShare(
        Guid id, Guid sourceTaskListId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, Guid? sharedTaskListId, Guid originalOwnerUserId)
    {
        Id = id;
        SourceTaskListId = sourceTaskListId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
        SharedTaskListId = sharedTaskListId;
        OriginalOwnerUserId = originalOwnerUserId;
    }

    public static TaskListShare Create(
        Guid sourceTaskListId, Guid ownerUserId, Guid recipientUserId, Guid originalOwnerUserId, ShareAccessLevel accessLevel = ShareAccessLevel.ReadOnly)
        => new(Guid.NewGuid(), sourceTaskListId, ownerUserId, recipientUserId, accessLevel, DateTimeOffset.UtcNow,
            acceptedAtUtc: null, sharedTaskListId: null, originalOwnerUserId);

    /// <summary>
    /// Rebuilds a share from already-persisted values, bypassing creation rules.
    /// </summary>
    public static TaskListShare FromPersistence(
        Guid id, Guid sourceTaskListId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, Guid? sharedTaskListId, Guid originalOwnerUserId)
        => new(id, sourceTaskListId, ownerUserId, recipientUserId, accessLevel, createdAtUtc, acceptedAtUtc, sharedTaskListId, originalOwnerUserId);

    /// <summary>
    /// No-op if already accepted, so accepting the same share twice (e.g. a duplicate click) never
    /// creates a second task list copy.
    /// </summary>
    public void MarkAccepted(Guid sharedTaskListId)
    {
        if (IsAccepted)
        {
            return;
        }

        AcceptedAtUtc = DateTimeOffset.UtcNow;
        SharedTaskListId = sharedTaskListId;
    }
}

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

    public bool IsAccepted => AcceptedAtUtc is not null;

    private TaskListShare(
        Guid id, Guid sourceTaskListId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, Guid? sharedTaskListId)
    {
        Id = id;
        SourceTaskListId = sourceTaskListId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
        SharedTaskListId = sharedTaskListId;
    }

    public static TaskListShare Create(
        Guid sourceTaskListId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel = ShareAccessLevel.ReadOnly)
        => new(Guid.NewGuid(), sourceTaskListId, ownerUserId, recipientUserId, accessLevel, DateTimeOffset.UtcNow,
            acceptedAtUtc: null, sharedTaskListId: null);

    /// <summary>
    /// Rebuilds a share from already-persisted values, bypassing creation rules.
    /// </summary>
    public static TaskListShare FromPersistence(
        Guid id, Guid sourceTaskListId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, Guid? sharedTaskListId)
        => new(id, sourceTaskListId, ownerUserId, recipientUserId, accessLevel, createdAtUtc, acceptedAtUtc, sharedTaskListId);

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

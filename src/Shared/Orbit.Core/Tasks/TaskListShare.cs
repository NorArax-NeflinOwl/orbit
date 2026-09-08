using Orbit.Core.Abstractions;

namespace Orbit.Core.Tasks;

/// <summary>
/// A grant of access to SourceTaskListId - mirrors Orbit.Core.Notes.NoteShare, see its class comment for
/// why accepting no longer copies the task list.
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

    /// <inheritdoc cref="Orbit.Core.Notes.NoteShare.IsPinnedByRecipient"/>
    public bool IsPinnedByRecipient { get; private set; }

    public bool IsAccepted => AcceptedAtUtc is not null;

    private TaskListShare(
        Guid id, Guid sourceTaskListId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, bool isPinnedByRecipient)
    {
        Id = id;
        SourceTaskListId = sourceTaskListId;
        OwnerUserId = ownerUserId;
        RecipientUserId = recipientUserId;
        AccessLevel = accessLevel;
        CreatedAtUtc = createdAtUtc;
        AcceptedAtUtc = acceptedAtUtc;
        IsPinnedByRecipient = isPinnedByRecipient;
    }

    public static TaskListShare Create(
        Guid sourceTaskListId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel = ShareAccessLevel.ReadOnly)
        => new(
            Guid.NewGuid(), sourceTaskListId, ownerUserId, recipientUserId, accessLevel, DateTimeOffset.UtcNow,
            acceptedAtUtc: null, isPinnedByRecipient: false);

    /// <summary>Rebuilds a share from already-persisted values, bypassing creation rules.</summary>
    public static TaskListShare FromPersistence(
        Guid id, Guid sourceTaskListId, Guid ownerUserId, Guid recipientUserId, ShareAccessLevel accessLevel,
        DateTimeOffset createdAtUtc, DateTimeOffset? acceptedAtUtc, bool isPinnedByRecipient = false)
        => new(
            id, sourceTaskListId, ownerUserId, recipientUserId, accessLevel, createdAtUtc, acceptedAtUtc,
            isPinnedByRecipient);

    /// <inheritdoc cref="Orbit.Core.Notes.NoteShare.SetPinnedByRecipient"/>
    public bool SetPinnedByRecipient(bool isPinned)
    {
        if (IsPinnedByRecipient == isPinned)
        {
            return false;
        }

        IsPinnedByRecipient = isPinned;
        return true;
    }

    /// <summary>No-op if already accepted, so accepting the same share twice (e.g. a duplicate click) is harmless.</summary>
    public void MarkAccepted()
    {
        AcceptedAtUtc ??= DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Raises what this share grants, and only ever raises it - answering a request for edit access is
    /// the point, and an owner re-sharing at a lower level than they already gave is far more likely to
    /// be a stale form than an intention to take access away. Returns whether anything changed.
    /// </summary>
    public bool RaiseAccessLevelTo(ShareAccessLevel accessLevel)
    {
        if (accessLevel <= AccessLevel)
        {
            return false;
        }

        AccessLevel = accessLevel;
        return true;
    }

}

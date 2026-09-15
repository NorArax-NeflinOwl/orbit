using Orbit.Core;
using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar;

/// <summary>
/// A single event, owned by exactly one user (<see cref="UserId"/>) for its entire lifetime - mirrors
/// Orbit.Core.Notes.Note, see its class comment for why IsShared/SharedByUserName/AccessLevel aren't
/// persisted.
/// </summary>
public sealed class CalendarEvent
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public CalendarEventDetails Details { get; private set; }

    /// <summary>
    /// The folder its owner filed it under, or null for one they have not filed anywhere - which is not
    /// "nowhere": an event with no folder is in Public. See Orbit.Core.Folders.BuiltInFolder.
    ///
    /// Beside <see cref="Details"/> rather than inside it, because filing is not part of what the event
    /// is: an update replaces the details wholesale, and a client that had not heard of folders would
    /// empty this every time it saved. See <see cref="MoveToFolder"/>, and Note.FolderId, which says the
    /// same about a note.
    /// </summary>
    public Guid? FolderId { get; private set; }

    /// <summary>
    /// Whether this appointment has been put away - see <see cref="Archive"/>. Stored rather than derived,
    /// unlike the other built-in folders (a sealed thing is private, a ticked-through list is finished),
    /// because there is nothing else about a appointment that could say it: being put away is a decision
    /// somebody makes about it rather than something it becomes.
    ///
    /// The owner's, and only theirs, exactly as <see cref="FolderId"/> is: one row is one appointment, so a
    /// recipient archiving it would be putting it away on its owner's own page.
    /// </summary>
    public bool IsArchived { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>The user id currently holding the edit lock, if any - see AcquireLock/ReleaseLock.</summary>
    public Guid? LockedByUserId { get; private set; }

    /// <summary>The locking user's login, captured at lock-acquisition time for display - meaningless when LockedByUserId is null.</summary>
    public string? LockedByUserName { get; private set; }

    /// <summary>Once past, the lock is treated as abandoned (e.g. a crashed tab) and anyone can acquire a fresh one.</summary>
    public DateTimeOffset? LockExpiresAtUtc { get; private set; }

    /// <summary>False for the owner, true for anyone viewing/editing this event through a share - see CalendarEventAccessResolver.</summary>
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

    private CalendarEvent(
        Guid id, Guid userId, CalendarEventDetails details, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc, Guid? folderId)
    {
        Id = id;
        UserId = userId;
        Details = details;
        FolderId = folderId;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        LockedByUserId = lockedByUserId;
        LockedByUserName = lockedByUserName;
        LockExpiresAtUtc = lockExpiresAtUtc;
    }

    public static CalendarEvent Create(Guid userId, CalendarEventDetails details, Guid? folderId = null)
    {
        ValidateTimeRange(details);
        ValidateTheWords(details);
        ValidateLocation(details);
        var now = DateTimeOffset.UtcNow;
        return new CalendarEvent(
            Guid.NewGuid(), userId, details, now, now,
            lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null, folderId);
    }

    /// <summary>Rebuilds an event from already-persisted values, bypassing creation rules.</summary>
    public static CalendarEvent FromPersistence(
        Guid id, Guid userId, CalendarEventDetails details, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc, Guid? folderId = null,
        bool isArchived = false)
        => new(id, userId, details, createdAtUtc, updatedAtUtc, lockedByUserId, lockedByUserName, lockExpiresAtUtc, folderId)
        {
            IsArchived = isArchived
        };

    /// <summary>Stamps how the current caller relates to this event - see the class comment. Not persisted.</summary>
    /// <summary>Tells the owner that somebody else holds accepted access - the mirror of <see cref="IsShared"/>.</summary>
    public void SetSharedWithOthers(bool isSharedWithOthers) => IsSharedWithOthers = isSharedWithOthers;

    public void SetAccessContext(bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel)
    {
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
        AccessLevel = accessLevel;
    }

    /// <summary>
    /// Callers are expected to have already checked AccessLevel is CanEdit and the event isn't locked by
    /// someone else - see UpdateCalendarEventCommandHandler.
    /// </summary>
    public void Update(CalendarEventDetails details)
    {
        ValidateTimeRange(details);
        ValidateTheWords(details);
        ValidateLocation(details);
        Details = details;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Files this event under a folder, or under none - which puts it back in Public. Its own step
    /// rather than part of <see cref="Update"/>, for the reason <see cref="FolderId"/> gives.
    /// </summary>
    public void MoveToFolder(Guid? folderId)
    {
        if (FolderId == folderId)
        {
            return;
        }

        FolderId = folderId;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Put away rather than thrown away - see Orbit.Core.Folders.BuiltInFolder.Archived, which is the
    /// tab this appointment then gathers under. The one way out of every list that is not deletion, and the
    /// answer to somebody who wants a appointment gone from in front of them without losing it.
    ///
    /// Its own command rather than a field on the update, for the reason <see cref="MoveToFolder"/>
    /// gives: an update replaces the whole thing, so a client that had not heard of archiving would
    /// bring back everything its owner had put away, every time it saved.
    ///
    /// <see cref="FolderId"/> is left exactly as it was. Archiving is not filing - it is a decision
    /// about whether this is in front of the reader at all - so bringing it back puts it under the
    /// folder it was under, rather than somewhere a rule had to choose.
    /// </summary>
    public void Archive(bool isArchived)
    {
        if (IsArchived == isArchived)
        {
            return;
        }

        IsArchived = isArchived;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
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

    /// <summary>
    /// What an event's words may be. Beside the time range because they are the same kind of rule -
    /// something the caller got wrong and can be told about - and because both have to run on creating
    /// an event and on changing one.
    /// </summary>
    private static void ValidateTheWords(CalendarEventDetails details)
    {
        StoredTextLimits.OrRefuse(details.Title, StoredTextLimits.Title, "event's title");
        StoredTextLimits.OrRefuseIfPresent(details.Description, StoredTextLimits.EventDescription, "event's description");
        StoredTextLimits.OrRefuseIfPresent(details.Color, StoredTextLimits.Color, "colour");
        StoredTextLimits.OrRefuseIfPresent(details.Location?.Address, StoredTextLimits.Address, "place's address");
    }

    private static void ValidateTimeRange(CalendarEventDetails details)
    {
        if (details.EndUtc < details.StartUtc)
        {
            throw new InvalidRequestException("An event's end time can't be before its start time.");
        }
    }

    private static void ValidateLocation(CalendarEventDetails details)
    {
        if (details.Location is not { } location)
        {
            return;
        }

        if (location.Latitude is < -90 or > 90)
        {
            throw new InvalidRequestException("A location's latitude must be between -90 and 90 degrees.");
        }

        if (location.Longitude is < -180 or > 180)
        {
            throw new InvalidRequestException("A location's longitude must be between -180 and 180 degrees.");
        }
    }
}

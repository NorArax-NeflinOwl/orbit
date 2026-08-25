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

    /// <summary>The current caller's access level - always CanEdit for the owner, and whatever their share grants otherwise.</summary>
    public ShareAccessLevel AccessLevel { get; private set; } = ShareAccessLevel.CanEdit;

    private CalendarEvent(
        Guid id, Guid userId, CalendarEventDetails details, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc)
    {
        Id = id;
        UserId = userId;
        Details = details;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        LockedByUserId = lockedByUserId;
        LockedByUserName = lockedByUserName;
        LockExpiresAtUtc = lockExpiresAtUtc;
    }

    public static CalendarEvent Create(Guid userId, CalendarEventDetails details)
    {
        ValidateTimeRange(details);
        ValidateLocation(details);
        var now = DateTimeOffset.UtcNow;
        return new CalendarEvent(Guid.NewGuid(), userId, details, now, now, lockedByUserId: null, lockedByUserName: null, lockExpiresAtUtc: null);
    }

    /// <summary>Rebuilds an event from already-persisted values, bypassing creation rules.</summary>
    public static CalendarEvent FromPersistence(
        Guid id, Guid userId, CalendarEventDetails details, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        Guid? lockedByUserId, string? lockedByUserName, DateTimeOffset? lockExpiresAtUtc)
        => new(id, userId, details, createdAtUtc, updatedAtUtc, lockedByUserId, lockedByUserName, lockExpiresAtUtc);

    /// <summary>Stamps how the current caller relates to this event - see the class comment. Not persisted.</summary>
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
        ValidateLocation(details);
        Details = details;
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

    private static void ValidateTimeRange(CalendarEventDetails details)
    {
        if (details.EndUtc < details.StartUtc)
        {
            throw new ArgumentException("An event's end time can't be before its start time.");
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
            throw new ArgumentException("A location's latitude must be between -90 and 90 degrees.");
        }

        if (location.Longitude is < -180 or > 180)
        {
            throw new ArgumentException("A location's longitude must be between -180 and 180 degrees.");
        }
    }
}

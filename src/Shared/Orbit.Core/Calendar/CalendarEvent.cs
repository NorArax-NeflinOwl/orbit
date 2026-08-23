using Orbit.Core.Abstractions;

namespace Orbit.Core.Calendar;

/// <summary>
/// A single event on a user's calendar.
/// </summary>
public sealed class CalendarEvent
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public CalendarEventDetails Details { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// True for a copy created by accepting another user's share offer (see CalendarEventShare and
    /// AcceptCalendarEventShareCommand) - false for an event the owner created themselves.
    /// <see cref="Update"/> refuses to change a shared copy whose <see cref="AccessLevel"/> is
    /// <see cref="ShareAccessLevel.ReadOnly"/>.
    /// </summary>
    public bool IsShared { get; private set; }

    /// <summary>The sharing user's login, captured once at share-acceptance time. Null when IsShared is false.</summary>
    public string? SharedByUserName { get; private set; }

    /// <summary>
    /// The access level the share was accepted under - meaningless when IsShared is false, where it's
    /// always ReadOnly. Captured once at acceptance time rather than looked up from the originating
    /// CalendarEventShare on every read, since the owner revoking access isn't a feature this app has.
    /// </summary>
    public ShareAccessLevel AccessLevel { get; private set; }

    /// <summary>
    /// The id of the user who first created this event, before any sharing - mirrors
    /// <see cref="Orbit.Core.Notes.Note.OriginalOwnerUserId"/>, see its comment for why this is needed
    /// and how it's threaded through re-shares.
    /// </summary>
    public Guid? OriginalOwnerUserId { get; private set; }

    /// <summary>The original owner regardless of how many times this event has been re-shared since.</summary>
    public Guid EffectiveOwnerUserId => IsShared ? OriginalOwnerUserId!.Value : UserId;

    private CalendarEvent(
        Guid id, Guid userId, CalendarEventDetails details, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel, Guid? originalOwnerUserId)
    {
        Id = id;
        UserId = userId;
        Details = details;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
        AccessLevel = accessLevel;
        OriginalOwnerUserId = originalOwnerUserId;
    }

    public static CalendarEvent Create(Guid userId, CalendarEventDetails details)
    {
        ValidateTimeRange(details);
        ValidateLocation(details);
        var now = DateTimeOffset.UtcNow;
        return new CalendarEvent(
            Guid.NewGuid(), userId, details, now, now,
            isShared: false, sharedByUserName: null, ShareAccessLevel.ReadOnly, originalOwnerUserId: null);
    }

    /// <summary>
    /// Creates recipientUserId's own copy of details once they accept a share - see
    /// AcceptCalendarEventShareCommandHandler. Nothing calls <see cref="Update"/> on the result when
    /// accessLevel isn't CanEdit, since it refuses to change such a copy's details anyway.
    /// </summary>
    public static CalendarEvent CreateShared(
        Guid recipientUserId, CalendarEventDetails details, string sharedByUserName, ShareAccessLevel accessLevel, Guid originalOwnerUserId)
    {
        ValidateTimeRange(details);
        ValidateLocation(details);
        var now = DateTimeOffset.UtcNow;
        return new CalendarEvent(Guid.NewGuid(), recipientUserId, details, now, now, isShared: true, sharedByUserName, accessLevel, originalOwnerUserId);
    }

    /// <summary>
    /// Rebuilds an event from already-persisted values, bypassing creation rules.
    /// </summary>
    public static CalendarEvent FromPersistence(
        Guid id, Guid userId, CalendarEventDetails details, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        bool isShared, string? sharedByUserName, ShareAccessLevel accessLevel, Guid? originalOwnerUserId)
        => new(id, userId, details, createdAtUtc, updatedAtUtc, isShared, sharedByUserName, accessLevel, originalOwnerUserId);

    public void Update(CalendarEventDetails details)
    {
        if (IsShared && AccessLevel != ShareAccessLevel.CanEdit)
        {
            throw new InvalidOperationException("A shared calendar event without CanEdit access can't be edited.");
        }

        ValidateTimeRange(details);
        ValidateLocation(details);
        Details = details;
        UpdatedAtUtc = DateTimeOffset.UtcNow;
    }

    private static void ValidateTimeRange(CalendarEventDetails details)
    {
        if (details.EndUtc < details.StartUtc)
        {
            throw new ArgumentException("An event's end time can't be before its start time.", nameof(details));
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
            throw new ArgumentException("A location's latitude must be between -90 and 90 degrees.", nameof(details));
        }

        if (location.Longitude is < -180 or > 180)
        {
            throw new ArgumentException("A location's longitude must be between -180 and 180 degrees.", nameof(details));
        }
    }
}

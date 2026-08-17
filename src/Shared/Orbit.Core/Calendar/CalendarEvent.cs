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
    /// True for a read-only copy created by accepting another user's share offer (see
    /// CalendarEventShare and AcceptCalendarEventShareCommand) - false for an event the owner created
    /// themselves. <see cref="Update"/> refuses to change a shared copy's details.
    /// </summary>
    public bool IsShared { get; private set; }

    /// <summary>The sharing user's login, captured once at share-acceptance time. Null when IsShared is false.</summary>
    public string? SharedByUserName { get; private set; }

    private CalendarEvent(
        Guid id, Guid userId, CalendarEventDetails details, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        bool isShared, string? sharedByUserName)
    {
        Id = id;
        UserId = userId;
        Details = details;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        IsShared = isShared;
        SharedByUserName = sharedByUserName;
    }

    public static CalendarEvent Create(Guid userId, CalendarEventDetails details)
    {
        ValidateTimeRange(details);
        ValidateLocation(details);
        var now = DateTimeOffset.UtcNow;
        return new CalendarEvent(Guid.NewGuid(), userId, details, now, now, isShared: false, sharedByUserName: null);
    }

    /// <summary>
    /// Creates recipientUserId's own read-only copy of details once they accept a share - see
    /// AcceptCalendarEventShareCommandHandler. Nothing calls <see cref="Update"/> on the result, since it
    /// refuses to change a shared event's details anyway.
    /// </summary>
    public static CalendarEvent CreateShared(Guid recipientUserId, CalendarEventDetails details, string sharedByUserName)
    {
        ValidateTimeRange(details);
        ValidateLocation(details);
        var now = DateTimeOffset.UtcNow;
        return new CalendarEvent(Guid.NewGuid(), recipientUserId, details, now, now, isShared: true, sharedByUserName);
    }

    /// <summary>
    /// Rebuilds an event from already-persisted values, bypassing creation rules.
    /// </summary>
    public static CalendarEvent FromPersistence(
        Guid id, Guid userId, CalendarEventDetails details, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc,
        bool isShared, string? sharedByUserName)
        => new(id, userId, details, createdAtUtc, updatedAtUtc, isShared, sharedByUserName);

    public void Update(CalendarEventDetails details)
    {
        if (IsShared)
        {
            throw new InvalidOperationException("A shared, read-only calendar event can't be edited.");
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

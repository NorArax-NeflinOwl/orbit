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

    private CalendarEvent(Guid id, Guid userId, CalendarEventDetails details, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
    {
        Id = id;
        UserId = userId;
        Details = details;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public static CalendarEvent Create(Guid userId, CalendarEventDetails details)
    {
        ValidateTimeRange(details);
        ValidateLocation(details);
        var now = DateTimeOffset.UtcNow;
        return new CalendarEvent(Guid.NewGuid(), userId, details, now, now);
    }

    /// <summary>
    /// Rebuilds an event from already-persisted values, bypassing creation rules.
    /// </summary>
    public static CalendarEvent FromPersistence(Guid id, Guid userId, CalendarEventDetails details, DateTimeOffset createdAtUtc, DateTimeOffset updatedAtUtc)
        => new(id, userId, details, createdAtUtc, updatedAtUtc);

    public void Update(CalendarEventDetails details)
    {
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

namespace Orbit.Data.Entities;

/// <summary>
/// Persistence shape of a calendar event, mapped separately from
/// <see cref="Orbit.Core.Calendar.CalendarEvent"/> so schema changes don't force changes onto domain
/// logic, and vice versa.
/// </summary>
public sealed class CalendarEventEntity
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Human-readable address resolved for <see cref="LocationLatitude"/>/<see cref="LocationLongitude"/>.</summary>
    public string? LocationAddress { get; set; }

    /// <summary>
    /// Null exactly when no location was picked - <see cref="LocationLongitude"/> is always set
    /// together with this (see <see cref="Orbit.Core.Calendar.EventLocation"/>).
    /// </summary>
    public double? LocationLatitude { get; set; }
    public double? LocationLongitude { get; set; }

    public string? Color { get; set; }
    public DateTimeOffset StartUtc { get; set; }
    public DateTimeOffset EndUtc { get; set; }
    public bool IsAllDay { get; set; }

    /// <summary>One of "Daily", "Weekly", "Monthly", or null for a non-repeating event.</summary>
    public string? RecurrenceFrequency { get; set; }
    public int? RecurrenceIntervalCount { get; set; }
    public DateTimeOffset? RecurrenceUntilUtc { get; set; }

    /// <summary>JSON-encoded list of guest email addresses - SQLite has no native array column type.</summary>
    public string GuestsJson { get; set; } = "[]";

    /// <summary>JSON-encoded list of reminder offsets, in minutes before the event start.</summary>
    public string RemindersJson { get; set; } = "[]";

    /// <summary>Whether an email is sent to the event's owner immediately when the event is first created.</summary>
    public bool NotifyOnCreation { get; set; }

    /// <summary>
    /// Whether the reminder emails configured via <see cref="RemindersJson"/> are actually sent as their
    /// lead time is reached - lets an owner keep configured lead times without clearing them while
    /// temporarily silencing "approaching event" notifications.
    /// </summary>
    public bool NotifyBeforeStart { get; set; }

    /// <summary>True for a read-only copy created by accepting a share offered by another user.</summary>
    public bool IsShared { get; set; }

    /// <summary>The sharing user's login, captured once at share-acceptance time. Null when IsShared is false.</summary>
    public string? SharedByUserName { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

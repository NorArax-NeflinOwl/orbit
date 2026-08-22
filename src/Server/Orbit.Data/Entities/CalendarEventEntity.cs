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

    /// <summary>
    /// One of "None", "Email", "Push", "Both" - which channel(s) get notified immediately when the
    /// event is first created (see Orbit.Core.Notifications.NotificationChannel).
    /// </summary>
    public string CreationNotificationChannel { get; set; } = "None";

    /// <summary>
    /// One of "None", "Email", "Push", "Both" - which channel(s) get notified as the reminder lead
    /// times configured via <see cref="RemindersJson"/> come due. Kept separate from
    /// <see cref="RemindersJson"/> itself, so an owner can keep configured lead times without clearing
    /// them while temporarily silencing "approaching event" notifications.
    /// </summary>
    public string ReminderNotificationChannel { get; set; } = "None";

    /// <summary>True for a copy created by accepting a share offered by another user.</summary>
    public bool IsShared { get; set; }

    /// <summary>The sharing user's login, captured once at share-acceptance time. Null when IsShared is false.</summary>
    public string? SharedByUserName { get; set; }

    /// <summary>"ReadOnly" or "CanEdit", captured once at share-acceptance time. Meaningless when IsShared is false.</summary>
    public string AccessLevel { get; set; } = "ReadOnly";

    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}
